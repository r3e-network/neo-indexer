// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Supabase.Client.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
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
    }
}
