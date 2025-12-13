// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.RestApi.Transport.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task SendTraceRequestWithRetryAsync(
            string requestUri,
            string apiKey,
            byte[] jsonPayload,
            string entityName)
        {
            var delay = TimeSpan.FromSeconds(1);
            const int maxAttempts = 5;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new ByteArrayContent(jsonPayload)
                };
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
                AddRestApiHeaders(request, apiKey);
                request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");

                using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return;

                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                        $"Supabase REST API throttled ({(int)response.StatusCode}) for {entityName} batch attempt {attempt}/{maxAttempts}. Retrying…");

                    if (attempt == maxAttempts)
                    {
                        var finalBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new InvalidOperationException(
                            $"REST API {entityName} upload failed after retries: {(int)response.StatusCode} {finalBody}");
                    }

                    await Task.Delay(delay).ConfigureAwait(false);
                    delay += delay;
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"REST API {entityName} upload failed: {(int)response.StatusCode} {body}");
            }
        }
    }
}

