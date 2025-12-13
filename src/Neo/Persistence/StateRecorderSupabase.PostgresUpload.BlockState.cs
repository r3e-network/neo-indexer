// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.BlockState.cs file belongs to the neo project and is free
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
		#region PostgreSQL Block Upload
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
		#endregion
#endif
	}
}

