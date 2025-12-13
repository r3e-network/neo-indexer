// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Traces.Trim.cs file belongs to the neo project and is free
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
#if NET9_0_OR_GREATER
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
#endif
    }
}

