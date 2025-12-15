// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.TransactionResults.cs file belongs to the neo project and is free
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
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
#endif

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
#if NET9_0_OR_GREATER
        private static Task UpsertTransactionResultsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            TransactionResultRow row)
        {
            return UpsertTransactionResultsPostgresAsync(connection, transaction, new[] { row }, batchSize: 1);
        }

        private static Task UpsertTransactionResultsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            IReadOnlyList<TransactionResultRow> rows,
            int batchSize)
        {
            if (rows is null) throw new ArgumentNullException(nameof(rows));
            if (rows.Count == 0) return Task.CompletedTask;

            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "vm_state",
                "vm_state_name",
                "success",
                "gas_consumed",
                "fault_exception",
                "result_stack_json",
                "opcode_count",
                "syscall_count",
                "contract_call_count",
                "storage_read_count",
                "storage_write_count",
                "notification_count",
                "log_count"
            };

            var values = new List<object?[]>(rows.Count);
            foreach (var row in rows)
            {
                values.Add(new object?[]
                {
                    row.BlockIndex,
                    row.TxHash,
                    row.VmState,
                    row.VmStateName,
                    row.Success,
                    row.GasConsumed,
                    row.FaultException,
                    row.ResultStackJson.HasValue ? row.ResultStackJson.Value.GetRawText() : null,
                    row.OpCodeCount,
                    row.SyscallCount,
                    row.ContractCallCount,
                    row.StorageReadCount,
                    row.StorageWriteCount,
                    row.NotificationCount,
                    row.LogCount
                });
            }

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "transaction_results",
                columns,
                "block_index, tx_hash",
                "vm_state = EXCLUDED.vm_state, vm_state_name = EXCLUDED.vm_state_name, success = EXCLUDED.success, gas_consumed = EXCLUDED.gas_consumed, fault_exception = EXCLUDED.fault_exception, result_stack_json = EXCLUDED.result_stack_json, opcode_count = EXCLUDED.opcode_count, syscall_count = EXCLUDED.syscall_count, contract_call_count = EXCLUDED.contract_call_count, storage_read_count = EXCLUDED.storage_read_count, storage_write_count = EXCLUDED.storage_write_count, notification_count = EXCLUDED.notification_count, log_count = EXCLUDED.log_count, updated_at = NOW()",
                values,
                batchSize);
        }
#endif
    }
}
