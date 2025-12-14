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

using System.Collections.Generic;
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
        private static async Task UploadBlockStatsPostgresAsync(BlockStats stats, StateRecorderSettings settings)
        {
            var values = new List<object?[]>(1) { BuildBlockStatsRow(stats) };

            await using var connection = new NpgsqlConnection(settings.SupabaseConnectionString);
            await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

            await UpsertRowsPostgresAsync(
                connection,
                transaction,
                "block_stats",
                BlockStatsColumns,
                "block_index",
                "tx_count = EXCLUDED.tx_count, total_gas_consumed = EXCLUDED.total_gas_consumed, opcode_count = EXCLUDED.opcode_count, syscall_count = EXCLUDED.syscall_count, contract_call_count = EXCLUDED.contract_call_count, storage_read_count = EXCLUDED.storage_read_count, storage_write_count = EXCLUDED.storage_write_count, notification_count = EXCLUDED.notification_count, log_count = EXCLUDED.log_count, updated_at = NOW()",
                values,
                batchSize: 1).ConfigureAwait(false);

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"PostgreSQL block stats upsert successful for block {stats.BlockIndex}");
        }
#endif
    }
}
