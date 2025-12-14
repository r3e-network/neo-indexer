// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.ReorgCleanup.RestApi.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task DeleteBlockDataRestApiAsync(int blockIndex, StateRecorderSettings settings)
        {
            var baseUrl = settings.SupabaseUrl.TrimEnd('/');
            var apiKey = settings.SupabaseApiKey;

            // Delete per-block read snapshot rows.
            await DeleteStorageReadsRestApiAsync(baseUrl, apiKey, blockIndex).ConfigureAwait(false);

            // Delete all trace rows for this block height. This handles reorgs where the tx set changes.
            await DeleteTraceRowsByBlockRestApiAsync(baseUrl, apiKey, "opcode_traces", blockIndex).ConfigureAwait(false);
            await DeleteTraceRowsByBlockRestApiAsync(baseUrl, apiKey, "syscall_traces", blockIndex).ConfigureAwait(false);
            await DeleteTraceRowsByBlockRestApiAsync(baseUrl, apiKey, "contract_calls", blockIndex).ConfigureAwait(false);
            await DeleteTraceRowsByBlockRestApiAsync(baseUrl, apiKey, "storage_writes", blockIndex).ConfigureAwait(false);
            await DeleteTraceRowsByBlockRestApiAsync(baseUrl, apiKey, "notifications", blockIndex).ConfigureAwait(false);
            await DeleteTraceRowsByBlockRestApiAsync(baseUrl, apiKey, "runtime_logs", blockIndex).ConfigureAwait(false);

            // transaction_results is keyed by (block_index, tx_hash) and must be deleted on reorg
            // to avoid stale tx rows lingering at the same height when the tx set changes.
            await DeleteTraceRowsByBlockRestApiAsync(baseUrl, apiKey, "transaction_results", blockIndex).ConfigureAwait(false);
        }

        private static async Task DeleteTraceRowsByBlockRestApiAsync(
            string baseUrl,
            string apiKey,
            string tableName,
            int blockIndex)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{baseUrl}/rest/v1/{tableName}?block_index=eq.{blockIndex}");
            AddRestApiHeaders(request, apiKey);

            using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"REST API trace delete failed: {tableName} {(int)response.StatusCode} {body}");
            }
        }
    }
}
