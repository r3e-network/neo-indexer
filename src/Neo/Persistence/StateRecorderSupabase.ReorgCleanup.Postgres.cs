// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.ReorgCleanup.Postgres.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
#endif

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static Task TryDeleteBlockDataPostgresAsync(int blockIndex, StateRecorderSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.SupabaseConnectionString))
                return Task.CompletedTask;

#if NET9_0_OR_GREATER
            return DeleteBlockDataPostgresAsync(blockIndex, settings);
#else
            return Task.CompletedTask;
#endif
        }

#if NET9_0_OR_GREATER
        private static async Task DeleteBlockDataPostgresAsync(int blockIndex, StateRecorderSettings settings)
        {
            await using var connection = new NpgsqlConnection(settings.SupabaseConnectionString);
            await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

            // storage_reads is not partitioned.
            await DeleteStorageReadsPostgresAsync(connection, transaction, blockIndex).ConfigureAwait(false);

            // Trace tables are partitioned by block_index. DELETE on parent will route to the correct partition.
            await DeleteTraceRowsPostgresAsync(connection, transaction, "opcode_traces", blockIndex).ConfigureAwait(false);
            await DeleteTraceRowsPostgresAsync(connection, transaction, "syscall_traces", blockIndex).ConfigureAwait(false);
            await DeleteTraceRowsPostgresAsync(connection, transaction, "contract_calls", blockIndex).ConfigureAwait(false);
            await DeleteTraceRowsPostgresAsync(connection, transaction, "storage_writes", blockIndex).ConfigureAwait(false);
            await DeleteTraceRowsPostgresAsync(connection, transaction, "notifications", blockIndex).ConfigureAwait(false);
            await DeleteTraceRowsPostgresAsync(connection, transaction, "transaction_results", blockIndex).ConfigureAwait(false);

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task DeleteTraceRowsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string tableName,
            int blockIndex)
        {
            await using var command = new NpgsqlCommand(
                $"DELETE FROM {tableName} WHERE block_index = @block_index",
                connection,
                transaction);
            command.Parameters.AddWithValue("block_index", blockIndex);
            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }
#endif
    }
}
