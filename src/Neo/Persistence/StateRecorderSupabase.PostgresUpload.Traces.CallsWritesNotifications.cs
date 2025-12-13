// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Traces.CallsWritesNotifications.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
#endif

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
#if NET9_0_OR_GREATER
        private static Task UpsertContractCallTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<ContractCallTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "trace_order",
                "caller_hash",
                "callee_hash",
                "method_name",
                "call_depth",
                "success",
                "gas_consumed"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.TraceOrder,
                r.CallerHash,
                r.CalleeHash,
                r.MethodName,
                r.CallDepth,
                r.Success,
                r.GasConsumed
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "contract_calls",
                columns,
                "block_index, tx_hash, trace_order",
                "caller_hash = EXCLUDED.caller_hash, callee_hash = EXCLUDED.callee_hash, method_name = EXCLUDED.method_name, call_depth = EXCLUDED.call_depth, success = EXCLUDED.success, gas_consumed = EXCLUDED.gas_consumed",
                values,
                batchSize);
        }

        private static Task UpsertStorageWriteTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<StorageWriteTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "write_order",
                "contract_id",
                "contract_hash",
                "key_base64",
                "old_value_base64",
                "new_value_base64"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.WriteOrder,
                r.ContractId,
                r.ContractHash,
                r.KeyBase64,
                r.OldValueBase64,
                r.NewValueBase64
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "storage_writes",
                columns,
                "block_index, tx_hash, write_order",
                "contract_id = EXCLUDED.contract_id, contract_hash = EXCLUDED.contract_hash, key_base64 = EXCLUDED.key_base64, old_value_base64 = EXCLUDED.old_value_base64, new_value_base64 = EXCLUDED.new_value_base64",
                values,
                batchSize);
        }

        private static Task UpsertNotificationTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<NotificationTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "notification_order",
                "contract_hash",
                "event_name",
                "state_json"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.NotificationOrder,
                r.ContractHash,
                r.EventName,
                r.StateJson.HasValue ? r.StateJson.Value.GetRawText() : null
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "notifications",
                columns,
                "block_index, tx_hash, notification_order",
                "contract_hash = EXCLUDED.contract_hash, event_name = EXCLUDED.event_name, state_json = EXCLUDED.state_json",
                values,
                batchSize);
        }
#endif
    }
}

