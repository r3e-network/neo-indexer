// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Rows.Insert.Sql.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Text;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
#if NET9_0_OR_GREATER
        private static string BuildInsertRowsSql(
            string tableName,
            string[] columns,
            string? conflictTarget,
            string? updateSet,
            int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var sb = new StringBuilder();
            sb.Append("INSERT INTO ").Append(tableName).Append(" (")
                .Append(string.Join(", ", columns)).Append(") VALUES ");

            for (var i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('(');
                for (var c = 0; c < columns.Length; c++)
                {
                    if (c > 0) sb.Append(", ");
                    sb.Append("@p").Append(i).Append('_').Append(c);
                }
                sb.Append(')');
            }

            if (!string.IsNullOrWhiteSpace(conflictTarget))
            {
                sb.Append(" ON CONFLICT (").Append(conflictTarget).Append(')');
                if (!string.IsNullOrWhiteSpace(updateSet))
                    sb.Append(" DO UPDATE SET ").Append(updateSet);
                else
                    sb.Append(" DO NOTHING");
            }

            return sb.ToString();
        }
#endif
    }
}

