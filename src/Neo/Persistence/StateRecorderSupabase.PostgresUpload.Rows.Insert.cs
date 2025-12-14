// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Rows.Insert.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
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
                var sql = BuildInsertRowsSql(tableName, columns, conflictTarget, updateSet, count);
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                AddInsertRowsParameters(command, columns, rows, offset, count);

                await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
#endif
    }
}
