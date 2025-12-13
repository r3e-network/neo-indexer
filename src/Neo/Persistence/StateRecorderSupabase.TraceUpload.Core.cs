// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.Core.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task UploadBlockTraceCoreAsync(
            uint blockIndex,
            string txHash,
            ExecutionTraceRecorder recorder,
            StateRecorderSettings settings)
        {
            var trimStaleTraceRows = settings.TrimStaleTraceRows;

            await TraceUploadLaneSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                var blockIndexValue = checked((int)blockIndex);
                var batchSize = GetTraceUploadBatchSize();

                var contractHashCache = new Dictionary<UInt160, string>();
                var opCodeRows = BuildOpCodeTraceRows(blockIndexValue, txHash, recorder.GetOpCodeTraces(), contractHashCache);
                var syscallRows = BuildSyscallTraceRows(blockIndexValue, txHash, recorder.GetSyscallTraces(), contractHashCache);
                var contractCallRows = BuildContractCallTraceRows(blockIndexValue, txHash, recorder.GetContractCallTraces(), contractHashCache);
                var storageWriteRows = BuildStorageWriteTraceRows(blockIndexValue, txHash, recorder.GetStorageWriteTraces(), contractHashCache);
                var notificationRows = BuildNotificationTraceRows(blockIndexValue, txHash, recorder.GetNotificationTraces(), contractHashCache);

                var useDirectPostgres = settings.Mode == StateRecorderSettings.UploadMode.Postgres || !settings.UploadEnabled;
                if (useDirectPostgres)
                {
                    if (string.IsNullOrWhiteSpace(settings.SupabaseConnectionString))
                        return;

#if NET9_0_OR_GREATER
                    await UploadBlockTracePostgresAsync(
                        blockIndexValue,
                        txHash,
                        opCodeRows,
                        syscallRows,
                        contractCallRows,
                        storageWriteRows,
                        notificationRows,
                        batchSize,
                        trimStaleTraceRows,
                        settings).ConfigureAwait(false);
#endif
                    return;
                }

                var uploaded = await UploadBlockTraceRestApiAsync(
                    baseUrl: settings.SupabaseUrl.TrimEnd('/'),
                    apiKey: settings.SupabaseApiKey,
                    opCodeRows,
                    syscallRows,
                    contractCallRows,
                    storageWriteRows,
                    notificationRows,
                    batchSize,
                    blockIndexValue,
                    txHash,
                    trimStaleTraceRows).ConfigureAwait(false);

                if (!uploaded)
                    return;

                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                    $"Trace upload successful for tx {txHash} @ block {blockIndex}: opcode={opCodeRows.Count}, syscall={syscallRows.Count}, calls={contractCallRows.Count}, writes={storageWriteRows.Count}, notifications={notificationRows.Count}");
            }
            finally
            {
                TraceUploadSemaphore.Release();
                TraceUploadLaneSemaphore.Release();
            }
        }
    }
}
