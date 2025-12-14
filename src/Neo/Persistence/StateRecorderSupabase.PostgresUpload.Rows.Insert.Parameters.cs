// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Rows.Insert.Parameters.cs file belongs to the neo project and is free
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
#if NET9_0_OR_GREATER
using Npgsql;
using NpgsqlTypes;
#endif

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
#if NET9_0_OR_GREATER
        private static void AddInsertRowsParameters(
            NpgsqlCommand command,
            string[] columns,
            IReadOnlyList<object?[]> rows,
            int offset,
            int count)
        {
            for (var i = 0; i < count; i++)
            {
                var row = rows[offset + i];
                for (var c = 0; c < columns.Length; c++)
                {
                    var parameter = command.Parameters.AddWithValue($"p{i}_{c}", row[c] ?? DBNull.Value);
                    if (columns[c] == "state_json" && row[c] is string)
                    {
                        parameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
                    }
                }
            }
        }
#endif
    }
}

