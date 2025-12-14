// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TransactionUpload.Core.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task UploadTransactionCoreAsync(
            uint blockIndex,
            string txHash,
            ExecutionTraceRecorder recorder,
            StateRecorderSettings settings,
            string expectedBlockHash,
            bool uploadTraces)
        {
            await TraceUploadLaneSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrWhiteSpace(expectedBlockHash) &&
                    TryGetCanonicalBlockHash(blockIndex, out var canonical) &&
                    !string.Equals(canonical, expectedBlockHash, StringComparison.Ordinal))
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                        $"Skipping tx upload for block {blockIndex}: block hash no longer canonical.");
                    return;
                }

                var backend = ResolveDatabaseBackend(settings.Mode, settings);
                if (backend == DatabaseBackend.None)
                    return;

                var blockIndexValue = checked((int)blockIndex);
                var batchSize = GetTraceUploadBatchSize();
                var trimStaleTraceRows = uploadTraces && settings.TrimStaleTraceRows;

                var txResultRow = BuildTransactionResultRow(blockIndexValue, txHash, recorder);

                if (backend == DatabaseBackend.Postgres)
                {
#if NET9_0_OR_GREATER
                    await UploadTransactionPostgresAsync(
                        txResultRow,
                        blockIndexValue,
                        txHash,
                        recorder,
                        settings,
                        uploadTraces,
                        batchSize,
                        trimStaleTraceRows).ConfigureAwait(false);
#endif
                    return;
                }

                await UploadTransactionRestApiAsync(
                    txResultRow,
                    blockIndexValue,
                    txHash,
                    recorder,
                    settings,
                    uploadTraces,
                    batchSize,
                    trimStaleTraceRows).ConfigureAwait(false);
            }
            finally
            {
                TraceUploadSemaphore.Release();
                TraceUploadLaneSemaphore.Release();
            }
        }
    }
}
