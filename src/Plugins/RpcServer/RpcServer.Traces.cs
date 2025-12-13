// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo;
using Neo.Extensions;
using Neo.Json;
using Neo.Persistence;
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private const int DefaultTraceLimit = 1000;
        private const int MaxTraceLimit = 5000;
        private const string RpcTracesSupabaseKeyEnvVar = "NEO_RPC_TRACES__SUPABASE_KEY";
        private const string RpcTracesConcurrencyEnvVar = "NEO_RPC_TRACES__MAX_CONCURRENCY";
        private const int DefaultRpcTracesConcurrency = 16;

        private static readonly int TraceQueryConcurrency = GetTraceQueryConcurrency();
        private static readonly SemaphoreSlim TraceQuerySemaphore = new(TraceQueryConcurrency, TraceQueryConcurrency);
        private static readonly HttpClient TraceHttpClient = CreateTraceHttpClient();
        private static readonly JsonSerializerOptions TraceSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static StateRecorderSettings EnsureSupabaseTraceSettings()
        {
            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled)
                throw new RpcException(RpcError.InternalServerError.WithData("state recorder not enabled"));
            if (string.IsNullOrWhiteSpace(settings.SupabaseUrl) || string.IsNullOrWhiteSpace(settings.SupabaseApiKey))
                throw new RpcException(RpcError.InternalServerError.WithData("supabase connection not configured"));

            var overrideKey = Environment.GetEnvironmentVariable(RpcTracesSupabaseKeyEnvVar);
            if (!string.IsNullOrWhiteSpace(overrideKey) && !string.Equals(overrideKey, settings.SupabaseApiKey, StringComparison.Ordinal))
            {
                settings = new StateRecorderSettings
                {
                    Enabled = settings.Enabled,
                    SupabaseUrl = settings.SupabaseUrl,
                    SupabaseApiKey = overrideKey,
                    SupabaseBucket = settings.SupabaseBucket,
                    SupabaseConnectionString = settings.SupabaseConnectionString,
                    Mode = settings.Mode,
                    TraceLevel = settings.TraceLevel,
                    TrimStaleTraceRows = settings.TrimStaleTraceRows,
                    UploadAuxFormats = settings.UploadAuxFormats,
                    MaxStorageReadsPerBlock = settings.MaxStorageReadsPerBlock
                };
            }
            return settings;
        }

        private async Task<SupabaseResponse<T>> SendSupabaseQueryAsync<T>(StateRecorderSettings settings, string resource, IEnumerable<KeyValuePair<string, string?>> queryParams)
        {
            var uri = BuildSupabaseUri(settings.SupabaseUrl, resource, queryParams);
            await TraceQuerySemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                ApplySupabaseHeaders(request, settings.SupabaseApiKey);

                using var response = await TraceHttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    throw new RpcException(RpcError.InternalServerError.WithData($"Supabase request failed ({(int)response.StatusCode}): {payload}"));

                List<T>? items;
                try
                {
                    items = JsonSerializer.Deserialize<List<T>>(payload, TraceSerializerOptions);
                }
                catch (JsonException ex)
                {
                    throw new RpcException(RpcError.InternalServerError.WithData($"Failed to parse Supabase response: {ex.Message}"));
                }

                var total = TryParseTotalCount(response) ?? items?.Count ?? 0;
                return new SupabaseResponse<T>(items ?? new List<T>(), total);
            }
            finally
            {
                TraceQuerySemaphore.Release();
            }
        }

        private async Task<IReadOnlyList<T>> SendSupabaseRpcAsync<T>(StateRecorderSettings settings, string functionName, object payload)
        {
            var uri = BuildSupabaseUri(settings.SupabaseUrl, $"rpc/{functionName}", Array.Empty<KeyValuePair<string, string?>>());
            var jsonBody = JsonSerializer.Serialize(payload, TraceSerializerOptions);

            await TraceQuerySemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };
                ApplySupabaseHeaders(request, settings.SupabaseApiKey);

                using var response = await TraceHttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    throw new RpcException(RpcError.InternalServerError.WithData($"Supabase RPC request failed ({(int)response.StatusCode}): {body}"));

                try
                {
                    return JsonSerializer.Deserialize<List<T>>(body, TraceSerializerOptions) ?? new List<T>();
                }
                catch (JsonException ex)
                {
                    throw new RpcException(RpcError.InternalServerError.WithData($"Failed to parse Supabase response: {ex.Message}"));
                }
            }
            finally
            {
                TraceQuerySemaphore.Release();
            }
        }

        private static int GetTraceQueryConcurrency()
        {
            var raw = Environment.GetEnvironmentVariable(RpcTracesConcurrencyEnvVar);
            if (string.IsNullOrWhiteSpace(raw))
                return DefaultRpcTracesConcurrency;

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return DefaultRpcTracesConcurrency;

            return parsed > 0 ? parsed : DefaultRpcTracesConcurrency;
        }

        private static HttpClient CreateTraceHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            return new HttpClient(handler);
        }

        private static string BuildSupabaseUri(string? baseUrl, string resource, IEnumerable<KeyValuePair<string, string?>> queryParams)
        {
            var trimmedBase = (baseUrl ?? string.Empty).TrimEnd('/');
            if (string.IsNullOrEmpty(trimmedBase))
                throw new RpcException(RpcError.InternalServerError.WithData("supabase url not configured"));

            StringBuilder builder = new();
            builder.Append(trimmedBase);
            builder.Append("/rest/v1/");
            builder.Append(resource);

            bool first = true;
            foreach (var (key, value) in queryParams)
            {
                if (string.IsNullOrEmpty(value))
                    continue;
                builder.Append(first ? '?' : '&');
                first = false;
                builder.Append(Uri.EscapeDataString(key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(value));
            }

            return builder.ToString();
        }

        private static void ApplySupabaseHeaders(HttpRequestMessage request, string apiKey)
        {
            request.Headers.TryAddWithoutValidation("apikey", apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        }

        private static int? TryParseTotalCount(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Content-Range", out var values))
                return null;
            var raw = values.FirstOrDefault();
            if (string.IsNullOrEmpty(raw))
                return null;
            var slashIndex = raw.LastIndexOf('/');
            if (slashIndex < 0 || slashIndex == raw.Length - 1)
                return null;
            var totalPart = raw[(slashIndex + 1)..];
            if (totalPart == "*")
                return null;
            return int.TryParse(totalPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var total) ? total : null;
        }

    }
}
