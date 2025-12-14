// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TransactionResultsUpload.Core.cs file belongs to the neo project and is free
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
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task UploadTransactionResultsCoreAsync(
            uint blockIndex,
            string expectedBlockHash,
            IReadOnlyCollection<ExecutionTraceRecorder> recorders,
            StateRecorderSettings settings)
        {
            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrWhiteSpace(expectedBlockHash) &&
                    TryGetCanonicalBlockHash(blockIndex, out var canonical) &&
                    !string.Equals(canonical, expectedBlockHash, StringComparison.Ordinal))
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                        $"Skipping transaction results upload for block {blockIndex}: block hash no longer canonical.");
                    return;
                }

                var backend = ResolveDatabaseBackend(settings.Mode, settings);
                if (backend == DatabaseBackend.None)
                    return;

                var blockIndexValue = checked((int)blockIndex);
                var batchSize = GetTraceUploadBatchSize();

                var rows = BuildTransactionResultRows(blockIndexValue, recorders);
                if (rows.Count == 0)
                    return;

                if (backend == DatabaseBackend.Postgres)
                {
#if NET9_0_OR_GREATER
                    await UploadTransactionResultsPostgresAsync(rows, settings, batchSize).ConfigureAwait(false);
#endif
                    return;
                }

                await UploadTransactionResultsRestApiAsync(rows, settings, batchSize).ConfigureAwait(false);

                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                    $"Transaction results upsert successful for block {blockIndex}: rows={rows.Count}");
            }
            finally
            {
                TraceUploadSemaphore.Release();
            }
        }

        private static List<TransactionResultRow> BuildTransactionResultRows(
            int blockIndex,
            IReadOnlyCollection<ExecutionTraceRecorder> recorders)
        {
            var rows = new List<TransactionResultRow>(recorders.Count);
            foreach (var recorder in recorders)
            {
                if (recorder is null)
                    continue;

                var txHash = recorder.TxHash?.ToString();
                if (string.IsNullOrWhiteSpace(txHash))
                    continue;

                rows.Add(BuildTransactionResultRow(blockIndex, txHash, recorder));
            }

            return rows;
        }
    }
}

