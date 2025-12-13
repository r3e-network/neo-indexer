// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Traces.cs file belongs to the neo project and is free
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
#if NET9_0_OR_GREATER
		#region PostgreSQL Trace Upload
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
		#endregion
#endif
	}
}

