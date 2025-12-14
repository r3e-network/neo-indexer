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

                var (opCodeRows, syscallRows, contractCallRows, storageWriteRows, notificationRows) =
                    BuildTraceRows(blockIndexValue, txHash, recorder);

                var hasRestApi = settings.UploadEnabled;
                var hasPostgres = !string.IsNullOrWhiteSpace(settings.SupabaseConnectionString);

                // Mirror block-state upload routing:
                // - Postgres mode prefers direct Postgres when configured, otherwise falls back to REST API.
                // - RestApi/Both prefer REST API when configured, otherwise fall back to direct Postgres.
                var usePostgres = false;
                if (settings.Mode == StateRecorderSettings.UploadMode.Postgres)
                {
                    if (hasPostgres)
                        usePostgres = true;
                    else if (!hasRestApi)
                        return;
                }
                else if (hasRestApi)
                {
                    usePostgres = false;
                }
                else if (hasPostgres)
                {
                    usePostgres = true;
                }
                else
                {
                    return;
                }

                if (usePostgres)
                {
                    await TryUploadBlockTracePostgresAsync(
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
