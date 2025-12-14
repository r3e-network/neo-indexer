// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.RestApi.Trim.cs file belongs to the neo project and is free
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
        #region Trace REST API (Trim)

        private static string? GetTraceOrderColumn(string tableName)
        {
            return tableName switch
            {
                "opcode_traces" => "trace_order",
                "syscall_traces" => "trace_order",
                "contract_calls" => "trace_order",
                "storage_writes" => "write_order",
                "notifications" => "notification_order",
                "runtime_logs" => "log_order",
                _ => null
            };
        }

        private static async Task UploadAndMaybeTrimTraceTableRestApiAsync<T>(
            string baseUrl,
            string apiKey,
            string tableName,
            string entityName,
            IReadOnlyList<T> rows,
            int batchSize,
            int blockIndex,
            string txHash,
            bool trimStaleRows)
        {
            if (rows.Count > 0)
            {
                await UploadTraceBatchRestApiAsync(baseUrl, apiKey, tableName, entityName, rows, batchSize).ConfigureAwait(false);
            }

            if (!trimStaleRows)
                return;

            var orderColumn = GetTraceOrderColumn(tableName);
            if (orderColumn is null)
                return;

            await DeleteTraceTailRestApiAsync(
                baseUrl,
                apiKey,
                tableName,
                entityName,
                blockIndex,
                txHash,
                orderColumn,
                keepCount: rows.Count).ConfigureAwait(false);
        }

        #endregion
    }
}
