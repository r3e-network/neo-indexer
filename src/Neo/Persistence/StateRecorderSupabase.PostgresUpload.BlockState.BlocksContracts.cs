// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.BlockState.BlocksContracts.cs file belongs to the neo project and is free
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
using System.Threading;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
#endif

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
#if NET9_0_OR_GREATER
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

        private static async Task UpsertContractsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            List<ContractRecord> contracts)
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
#endif
    }
}

