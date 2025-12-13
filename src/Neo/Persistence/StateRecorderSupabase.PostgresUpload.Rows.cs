// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Rows.cs file belongs to the neo project and is free
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
		#region PostgreSQL Insert Helpers
		private static async Task UpsertRowsPostgresAsync(
			NpgsqlConnection connection,
			NpgsqlTransaction transaction,
			string tableName,
			string[] columns,
			string conflictTarget,
			string updateSet,
			IReadOnlyList<object?[]> rows,
			int batchSize)
		{
			await InsertRowsPostgresAsync(
				connection,
				transaction,
				tableName,
				columns,
				conflictTarget,
				updateSet,
				rows,
				batchSize).ConfigureAwait(false);
		}

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

				await using var command = new NpgsqlCommand(sb.ToString(), connection, transaction);

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

				await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
			}
		}
		#endregion
#endif
	}
}

