// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TransactionResultsUpload.Core.Postgres.cs file belongs to the neo project and is free
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
        private static async Task UploadTransactionResultsPostgresAsync(
            IReadOnlyList<TransactionResultRow> rows,
            StateRecorderSettings settings,
            int batchSize)
        {
            await using var connection = new NpgsqlConnection(settings.SupabaseConnectionString);
            await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

            await UpsertTransactionResultsPostgresAsync(
                connection,
                transaction,
                rows,
                batchSize: batchSize).ConfigureAwait(false);

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        }
#endif
    }
}

