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
			#endregion
	#endif
		}
	}
