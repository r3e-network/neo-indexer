// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Core.cs file belongs to the neo project and is free
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
		#region PostgreSQL Direct Upload
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
		#endregion
#endif
	}
}

