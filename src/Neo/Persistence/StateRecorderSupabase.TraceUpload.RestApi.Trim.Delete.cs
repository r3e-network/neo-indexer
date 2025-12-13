// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.RestApi.Trim.Delete.cs file belongs to the neo project and is free
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
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task DeleteTraceTailRestApiAsync(
            string baseUrl,
            string apiKey,
            string tableName,
            string entityName,
            int blockIndex,
            string txHash,
            string orderColumn,
            int keepCount)
        {
            var escapedTxHash = Uri.EscapeDataString(txHash);
            var requestUri =
                $"{baseUrl}/rest/v1/{tableName}?block_index=eq.{blockIndex}&tx_hash=eq.{escapedTxHash}&{orderColumn}=gte.{keepCount}";

            var delay = TimeSpan.FromSeconds(1);
            const int maxAttempts = 5;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
                AddRestApiHeaders(request, apiKey);
                request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");

                using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                    return;

                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                        $"Supabase REST API throttled ({(int)response.StatusCode}) while trimming {entityName} attempt {attempt}/{maxAttempts}. Retrying…");

                    if (attempt == maxAttempts)
                    {
                        var finalBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new InvalidOperationException(
                            $"REST API {entityName} trim failed after retries: {(int)response.StatusCode} {finalBody}");
                    }

                    await Task.Delay(delay).ConfigureAwait(false);
                    delay += delay;
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || IsUpsertPermissionError(body))
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                        $"Supabase REST API cannot trim stale {entityName} for tx {txHash} @ block {blockIndex}: {(int)response.StatusCode} {body}");
                    return;
                }

                throw new InvalidOperationException($"REST API {entityName} trim failed: {(int)response.StatusCode} {body}");
            }
        }
    }
}

