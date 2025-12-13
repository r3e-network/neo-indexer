// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Rows.Upsert.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
#endif

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
#if NET9_0_OR_GREATER
        private static Task UpsertRowsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string tableName,
            string[] columns,
            string conflictTarget,
            string updateSet,
            IReadOnlyList<object?[]> rows,
            int batchSize)
        {
            return InsertRowsPostgresAsync(
                connection,
                transaction,
                tableName,
                columns,
                conflictTarget,
                updateSet,
                rows,
                batchSize);
        }
#endif
    }
}

