// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.RestApi.Orchestrator.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task<bool> UploadBlockTraceRestApiAsync(
            string baseUrl,
            string apiKey,
            IReadOnlyList<OpCodeTraceRow> opCodeRows,
            IReadOnlyList<SyscallTraceRow> syscallRows,
            IReadOnlyList<ContractCallTraceRow> contractCallRows,
            IReadOnlyList<StorageWriteTraceRow> storageWriteRows,
            IReadOnlyList<NotificationTraceRow> notificationRows,
            int batchSize,
            int blockIndex,
            string txHash,
            bool trimStaleTraceRows)
        {
            // Upload sequentially to avoid multiplying concurrent Supabase REST requests per transaction.
            // Global concurrency is controlled by TraceUploadSemaphore; keep per-tx traffic predictable.
            var any = false;

            if (trimStaleTraceRows || opCodeRows.Count > 0)
            {
                any = true;
                await UploadAndMaybeTrimTraceTableRestApiAsync(
                    baseUrl,
                    apiKey,
                    tableName: "opcode_traces",
                    entityName: "opcode traces",
                    opCodeRows,
                    batchSize,
                    blockIndex,
                    txHash,
                    trimStaleTraceRows).ConfigureAwait(false);
            }

            if (trimStaleTraceRows || syscallRows.Count > 0)
            {
                any = true;
                await UploadAndMaybeTrimTraceTableRestApiAsync(
                    baseUrl,
                    apiKey,
                    tableName: "syscall_traces",
                    entityName: "syscall traces",
                    syscallRows,
                    batchSize,
                    blockIndex,
                    txHash,
                    trimStaleTraceRows).ConfigureAwait(false);
            }

            if (trimStaleTraceRows || contractCallRows.Count > 0)
            {
                any = true;
                await UploadAndMaybeTrimTraceTableRestApiAsync(
                    baseUrl,
                    apiKey,
                    tableName: "contract_calls",
                    entityName: "contract call traces",
                    contractCallRows,
                    batchSize,
                    blockIndex,
                    txHash,
                    trimStaleTraceRows).ConfigureAwait(false);
            }

            if (trimStaleTraceRows || storageWriteRows.Count > 0)
            {
                any = true;
                await UploadAndMaybeTrimTraceTableRestApiAsync(
                    baseUrl,
                    apiKey,
                    tableName: "storage_writes",
                    entityName: "storage write traces",
                    storageWriteRows,
                    batchSize,
                    blockIndex,
                    txHash,
                    trimStaleTraceRows).ConfigureAwait(false);
            }

            if (trimStaleTraceRows || notificationRows.Count > 0)
            {
                any = true;
                await UploadAndMaybeTrimTraceTableRestApiAsync(
                    baseUrl,
                    apiKey,
                    tableName: "notifications",
                    entityName: "notification traces",
                    notificationRows,
                    batchSize,
                    blockIndex,
                    txHash,
                    trimStaleTraceRows).ConfigureAwait(false);
            }

            return any;
        }
    }
}
