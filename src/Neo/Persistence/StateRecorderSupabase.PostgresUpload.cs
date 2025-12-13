// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Extensions;
using Neo.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
using NpgsqlTypes;
#endif


namespace Neo.Persistence
{
	public static partial class StateRecorderSupabase
	{
        #region PostgreSQL Direct Upload

#if NET9_0_OR_GREATER
        private static async Task UploadPostgresAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            var entries = GetOrderedEntries(recorder);
            var blockRecord = BuildBlockRecord(recorder, entries);
            var storageReads = BuildStorageReadRecords(recorder, entries);
            var contracts = BuildContractRecords(entries);

            await using var connection = new NpgsqlConnection(settings.SupabaseConnectionString);
            await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

            await UpsertBlockPostgresAsync(connection, transaction, blockRecord).ConfigureAwait(false);

            if (contracts.Count > 0)
            {
                await UpsertContractsPostgresAsync(connection, transaction, contracts).ConfigureAwait(false);
            }

            if (storageReads.Count > 0)
            {
                try
                {
                    await UpsertStorageReadsPostgresAsync(connection, transaction, storageReads).ConfigureAwait(false);
                }
                catch (PostgresException ex) when (ex.SqlState == "42P10")
                {
                    // Older schemas (pre migration 012) cannot upsert storage_reads.
                    await DeleteStorageReadsPostgresAsync(connection, transaction, blockRecord.BlockIndex).ConfigureAwait(false);
                    await InsertStorageReadsPostgresAsync(connection, transaction, storageReads).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"PostgreSQL upsert successful for block {recorder.BlockIndex}: {storageReads.Count} reads, {contracts.Count} new contracts");
        }

        private static async Task DeleteStorageReadsPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int blockIndex)
        {
            await using var command = new NpgsqlCommand(
                "DELETE FROM storage_reads WHERE block_index = @block_index",
                connection,
                transaction);
            command.Parameters.AddWithValue("block_index", blockIndex);
            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task UpsertBlockPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, BlockRecord block)
        {
            const string sql = @"
INSERT INTO blocks (block_index, hash, timestamp_ms, tx_count, read_key_count)
VALUES (@block_index, @hash, @timestamp_ms, @tx_count, @read_key_count)
ON CONFLICT (block_index) DO UPDATE SET
    hash = EXCLUDED.hash,
    timestamp_ms = EXCLUDED.timestamp_ms,
    tx_count = EXCLUDED.tx_count,
    read_key_count = EXCLUDED.read_key_count,
    updated_at = NOW();";

            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("block_index", block.BlockIndex);
            command.Parameters.AddWithValue("hash", block.Hash);
            command.Parameters.AddWithValue("timestamp_ms", block.TimestampMs);
            command.Parameters.AddWithValue("tx_count", block.TransactionCount);
            command.Parameters.AddWithValue("read_key_count", block.ReadKeyCount);
            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task UpsertContractsPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<ContractRecord> contracts)
        {
            var columns = new[] { "contract_id", "contract_hash", "manifest_name" };
            var values = contracts.Select(c => new object?[]
            {
                c.ContractId,
                c.ContractHash,
                c.ManifestName
            }).ToList();

            await UpsertRowsPostgresAsync(
                connection,
                transaction,
                "contracts",
                columns,
                "contract_id",
                "contract_hash = EXCLUDED.contract_hash, manifest_name = EXCLUDED.manifest_name, updated_at = NOW()",
                values,
                batchSize: 500).ConfigureAwait(false);

            foreach (var contract in contracts)
            {
                ContractCache.TryAdd(contract.ContractId, contract);
            }
        }

        private static async Task UpsertStorageReadsPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<StorageReadRecord> reads)
        {
            var columns = new[]
            {
                "block_index",
                "contract_id",
                "key_base64",
                "value_base64",
                "read_order",
                "tx_hash",
                "source"
            };

            var values = reads.Select(r => new object?[]
            {
                r.BlockIndex,
                r.ContractId,
                r.KeyBase64,
                r.ValueBase64,
                r.ReadOrder,
                r.TxHash,
                r.Source
            }).ToList();

            await UpsertRowsPostgresAsync(
                connection,
                transaction,
                "storage_reads",
                columns,
                "block_index, contract_id, key_base64",
                "value_base64 = EXCLUDED.value_base64, read_order = EXCLUDED.read_order, tx_hash = EXCLUDED.tx_hash, source = EXCLUDED.source",
                values,
                StorageReadBatchSize).ConfigureAwait(false);
        }

        private static async Task InsertStorageReadsPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<StorageReadRecord> reads)
        {
            var columns = new[]
            {
                "block_index",
                "contract_id",
                "key_base64",
                "value_base64",
                "read_order",
                "tx_hash",
                "source"
            };

            var values = reads.Select(r => new object?[]
            {
                r.BlockIndex,
                r.ContractId,
                r.KeyBase64,
                r.ValueBase64,
                r.ReadOrder,
                r.TxHash,
                r.Source
            }).ToList();

            await InsertRowsPostgresAsync(
                connection,
                transaction,
                "storage_reads",
                columns,
                conflictTarget: null,
                updateSet: null,
                values,
                StorageReadBatchSize).ConfigureAwait(false);
        }

        private static async Task UpsertRowsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string tableName,
            string[] columns,
            string conflictTarget,
            string updateSet,
            IReadOnlyList<object?[]> rows,
            int batchSize)
        {
            await InsertRowsPostgresAsync(
                connection,
                transaction,
                tableName,
                columns,
                conflictTarget,
                updateSet,
                rows,
                batchSize).ConfigureAwait(false);
        }

        private static async Task InsertRowsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string tableName,
            string[] columns,
            string? conflictTarget,
            string? updateSet,
            IReadOnlyList<object?[]> rows,
            int batchSize)
        {
            if (rows.Count == 0)
                return;

            var effectiveBatchSize = batchSize > 0 ? batchSize : rows.Count;

            for (var offset = 0; offset < rows.Count; offset += effectiveBatchSize)
            {
                var count = Math.Min(effectiveBatchSize, rows.Count - offset);
                var sb = new StringBuilder();
                sb.Append("INSERT INTO ").Append(tableName).Append(" (")
                  .Append(string.Join(", ", columns)).Append(") VALUES ");

                for (var i = 0; i < count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('(');
                    for (var c = 0; c < columns.Length; c++)
                    {
                        if (c > 0) sb.Append(", ");
                        sb.Append("@p").Append(i).Append('_').Append(c);
                    }
                    sb.Append(')');
                }

                if (!string.IsNullOrWhiteSpace(conflictTarget))
                {
                    sb.Append(" ON CONFLICT (").Append(conflictTarget).Append(')');
                    if (!string.IsNullOrWhiteSpace(updateSet))
                        sb.Append(" DO UPDATE SET ").Append(updateSet);
                    else
                        sb.Append(" DO NOTHING");
                }

                await using var command = new NpgsqlCommand(sb.ToString(), connection, transaction);

                for (var i = 0; i < count; i++)
                {
                    var row = rows[offset + i];
                    for (var c = 0; c < columns.Length; c++)
                    {
                        var parameter = command.Parameters.AddWithValue($"p{i}_{c}", row[c] ?? DBNull.Value);
                        if (columns[c] == "state_json" && row[c] is string)
                        {
                            parameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
                        }
                    }
                }

                await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

	        private static async Task UploadBlockTracePostgresAsync(
	            int blockIndex,
	            string txHash,
	            List<OpCodeTraceRow> opCodeRows,
	            List<SyscallTraceRow> syscallRows,
	            List<ContractCallTraceRow> contractCallRows,
	            List<StorageWriteTraceRow> storageWriteRows,
	            List<NotificationTraceRow> notificationRows,
	            int batchSize,
	            bool trimStaleRows,
	            StateRecorderSettings settings)
	        {
	            await using var connection = new NpgsqlConnection(settings.SupabaseConnectionString);
	            await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
	            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

            if (opCodeRows.Count > 0)
                await UpsertOpCodeTracesPostgresAsync(connection, transaction, opCodeRows, batchSize).ConfigureAwait(false);

            if (syscallRows.Count > 0)
                await UpsertSyscallTracesPostgresAsync(connection, transaction, syscallRows, batchSize).ConfigureAwait(false);

            if (contractCallRows.Count > 0)
                await UpsertContractCallTracesPostgresAsync(connection, transaction, contractCallRows, batchSize).ConfigureAwait(false);

            if (storageWriteRows.Count > 0)
                await UpsertStorageWriteTracesPostgresAsync(connection, transaction, storageWriteRows, batchSize).ConfigureAwait(false);

	            if (notificationRows.Count > 0)
	                await UpsertNotificationTracesPostgresAsync(connection, transaction, notificationRows, batchSize).ConfigureAwait(false);

	            if (trimStaleRows)
	            {
	                try
	                {
	                    await TrimTraceTailPostgresAsync(connection, transaction, "opcode_traces", "trace_order", blockIndex, txHash, opCodeRows.Count).ConfigureAwait(false);
	                    await TrimTraceTailPostgresAsync(connection, transaction, "syscall_traces", "trace_order", blockIndex, txHash, syscallRows.Count).ConfigureAwait(false);
	                    await TrimTraceTailPostgresAsync(connection, transaction, "contract_calls", "trace_order", blockIndex, txHash, contractCallRows.Count).ConfigureAwait(false);
	                    await TrimTraceTailPostgresAsync(connection, transaction, "storage_writes", "write_order", blockIndex, txHash, storageWriteRows.Count).ConfigureAwait(false);
	                    await TrimTraceTailPostgresAsync(connection, transaction, "notifications", "notification_order", blockIndex, txHash, notificationRows.Count).ConfigureAwait(false);
	                }
	                catch (Exception ex)
	                {
	                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
	                        $"PostgreSQL trace trim failed for tx {txHash} @ block {blockIndex}: {ex.Message}");
	                }
	            }

	            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

	            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
	                $"PostgreSQL trace upload successful for tx {txHash} @ block {blockIndex}: opcode={opCodeRows.Count}, syscall={syscallRows.Count}, calls={contractCallRows.Count}, writes={storageWriteRows.Count}, notifications={notificationRows.Count}");
	        }

	        private static async Task TrimTraceTailPostgresAsync(
	            NpgsqlConnection connection,
	            NpgsqlTransaction transaction,
	            string tableName,
	            string orderColumn,
	            int blockIndex,
	            string txHash,
	            int keepCount)
	        {
	            var sql = $"DELETE FROM {tableName} WHERE block_index = @block_index AND tx_hash = @tx_hash AND {orderColumn} >= @keep_count";
	            await using var command = new NpgsqlCommand(sql, connection, transaction);
	            command.Parameters.AddWithValue("block_index", blockIndex);
	            command.Parameters.AddWithValue("tx_hash", txHash);
	            command.Parameters.AddWithValue("keep_count", keepCount);
	            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
	        }

        private static Task UpsertOpCodeTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<OpCodeTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "trace_order",
                "contract_hash",
                "instruction_pointer",
                "opcode",
                "opcode_name",
                "operand_base64",
                "gas_consumed",
                "stack_depth"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.TraceOrder,
                r.ContractHash,
                r.InstructionPointer,
                r.OpCode,
                r.OpCodeName,
                r.OperandBase64,
                r.GasConsumed,
                r.StackDepth
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "opcode_traces",
                columns,
                "block_index, tx_hash, trace_order",
                "contract_hash = EXCLUDED.contract_hash, instruction_pointer = EXCLUDED.instruction_pointer, opcode = EXCLUDED.opcode, opcode_name = EXCLUDED.opcode_name, operand_base64 = EXCLUDED.operand_base64, gas_consumed = EXCLUDED.gas_consumed, stack_depth = EXCLUDED.stack_depth",
                values,
                batchSize);
        }

        private static Task UpsertSyscallTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<SyscallTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "trace_order",
                "contract_hash",
                "syscall_hash",
                "syscall_name",
                "gas_cost"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.TraceOrder,
                r.ContractHash,
                r.SyscallHash,
                r.SyscallName,
                r.GasCost
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "syscall_traces",
                columns,
                "block_index, tx_hash, trace_order",
                "contract_hash = EXCLUDED.contract_hash, syscall_hash = EXCLUDED.syscall_hash, syscall_name = EXCLUDED.syscall_name, gas_cost = EXCLUDED.gas_cost",
                values,
                batchSize);
        }

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

        private static async Task UploadBlockStatsPostgresAsync(BlockStats stats, StateRecorderSettings settings)
        {
            var columns = new[]
            {
                "block_index",
                "tx_count",
                "total_gas_consumed",
                "opcode_count",
                "syscall_count",
                "contract_call_count",
                "storage_read_count",
                "storage_write_count",
                "notification_count"
            };

            var values = new List<object?[]>(1)
            {
                new object?[]
                {
                    checked((int)stats.BlockIndex),
                    stats.TransactionCount,
                    stats.TotalGasConsumed,
                    stats.OpCodeCount,
                    stats.SyscallCount,
                    stats.ContractCallCount,
                    stats.StorageReadCount,
                    stats.StorageWriteCount,
                    stats.NotificationCount
                }
            };

            await using var connection = new NpgsqlConnection(settings.SupabaseConnectionString);
            await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

            await UpsertRowsPostgresAsync(
                connection,
                transaction,
                "block_stats",
                columns,
                "block_index",
                "tx_count = EXCLUDED.tx_count, total_gas_consumed = EXCLUDED.total_gas_consumed, opcode_count = EXCLUDED.opcode_count, syscall_count = EXCLUDED.syscall_count, contract_call_count = EXCLUDED.contract_call_count, storage_read_count = EXCLUDED.storage_read_count, storage_write_count = EXCLUDED.storage_write_count, notification_count = EXCLUDED.notification_count, updated_at = NOW()",
                values,
                batchSize: 1).ConfigureAwait(false);

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"PostgreSQL block stats upsert successful for block {stats.BlockIndex}");
        }
#else
        private static Task UploadPostgresAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                "PostgreSQL direct upload requires net9.0 or greater.");
            return Task.CompletedTask;
        }
#endif

        #endregion
	}
}
