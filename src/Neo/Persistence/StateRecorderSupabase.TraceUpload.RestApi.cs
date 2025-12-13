// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.RestApi.cs file belongs to the neo project and is free
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
        private static Task UploadOpCodeTracesRestApiAsync(string baseUrl, string apiKey, IReadOnlyList<OpCodeTraceRow> rows, int batchSize)
        {
            return UploadTraceBatchRestApiAsync(baseUrl, apiKey, "opcode_traces", "opcode traces", rows, batchSize);
        }

        private static Task UploadSyscallTracesRestApiAsync(string baseUrl, string apiKey, IReadOnlyList<SyscallTraceRow> rows, int batchSize)
        {
            return UploadTraceBatchRestApiAsync(baseUrl, apiKey, "syscall_traces", "syscall traces", rows, batchSize);
        }

        private static Task UploadContractCallTracesRestApiAsync(string baseUrl, string apiKey, IReadOnlyList<ContractCallTraceRow> rows, int batchSize)
        {
            return UploadTraceBatchRestApiAsync(baseUrl, apiKey, "contract_calls", "contract call traces", rows, batchSize);
        }

        private static Task UploadStorageWriteTracesRestApiAsync(string baseUrl, string apiKey, IReadOnlyList<StorageWriteTraceRow> rows, int batchSize)
        {
            return UploadTraceBatchRestApiAsync(baseUrl, apiKey, "storage_writes", "storage write traces", rows, batchSize);
        }

        private static Task UploadNotificationTracesRestApiAsync(string baseUrl, string apiKey, IReadOnlyList<NotificationTraceRow> rows, int batchSize)
        {
            return UploadTraceBatchRestApiAsync(baseUrl, apiKey, "notifications", "notification traces", rows, batchSize);
        }

        private static string? GetTraceUpsertConflictTarget(string tableName)
        {
            return tableName switch
            {
                "opcode_traces" => "block_index,tx_hash,trace_order",
                "syscall_traces" => "block_index,tx_hash,trace_order",
                "contract_calls" => "block_index,tx_hash,trace_order",
                "storage_writes" => "block_index,tx_hash,write_order",
                "notifications" => "block_index,tx_hash,notification_order",
                _ => null
            };
        }
    }
}

