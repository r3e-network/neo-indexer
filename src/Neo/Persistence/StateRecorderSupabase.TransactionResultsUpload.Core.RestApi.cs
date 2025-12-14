// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TransactionResultsUpload.Core.RestApi.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task UploadTransactionResultsRestApiAsync(
            IReadOnlyList<TransactionResultRow> rows,
            StateRecorderSettings settings,
            int batchSize)
        {
            if (rows.Count == 0)
                return;

            var baseUrl = settings.SupabaseUrl.TrimEnd('/');
            var apiKey = settings.SupabaseApiKey;

            var effectiveBatchSize = batchSize > 0 ? batchSize : rows.Count;

            for (var offset = 0; offset < rows.Count; offset += effectiveBatchSize)
            {
                var count = Math.Min(effectiveBatchSize, rows.Count - offset);
                var batch = new TransactionResultRow[count];
                for (var i = 0; i < count; i++)
                    batch[i] = rows[offset + i];

                var payload = JsonSerializer.SerializeToUtf8Bytes(batch);
                await SendTraceRequestWithRetryAsync(
                    $"{baseUrl}/rest/v1/transaction_results?on_conflict=block_index,tx_hash",
                    apiKey,
                    payload,
                    "transaction results").ConfigureAwait(false);
            }
        }
    }
}

