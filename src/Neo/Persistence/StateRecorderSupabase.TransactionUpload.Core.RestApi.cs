// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TransactionUpload.Core.RestApi.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Text.Json;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task UploadTransactionRestApiAsync(
            TransactionResultRow txResultRow,
            int blockIndex,
            string txHash,
            ExecutionTraceRecorder recorder,
            StateRecorderSettings settings,
            bool uploadTraces,
            int batchSize,
            bool trimStaleTraceRows)
        {
            var baseUrl = settings.SupabaseUrl.TrimEnd('/');
            var apiKey = settings.SupabaseApiKey;

            var txPayload = JsonSerializer.SerializeToUtf8Bytes(new[] { txResultRow });
            await SendTraceRequestWithRetryAsync(
                $"{baseUrl}/rest/v1/transaction_results?on_conflict=block_index,tx_hash",
                apiKey,
                txPayload,
                "transaction results").ConfigureAwait(false);

            if (!uploadTraces)
                return;

            var (opCodeRows, syscallRows, contractCallRows, storageWriteRows, notificationRows) =
                BuildTraceRows(blockIndex, txHash, recorder);

            await UploadBlockTraceRestApiAsync(
                baseUrl,
                apiKey,
                opCodeRows,
                syscallRows,
                contractCallRows,
                storageWriteRows,
                notificationRows,
                batchSize,
                blockIndex,
                txHash,
                trimStaleTraceRows).ConfigureAwait(false);
        }
    }
}

