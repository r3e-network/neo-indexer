// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.BlockStats.cs file belongs to the neo project and is free
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
		#region PostgreSQL Block Stats
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
		#endregion
#endif
	}
}

