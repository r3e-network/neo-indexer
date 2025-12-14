// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Traces.cs file belongs to the neo project and is free
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
        private static async Task UploadBlockTracePostgresAsync(
            int blockIndex,
            string txHash,
            List<OpCodeTraceRow> opCodeRows,
            List<SyscallTraceRow> syscallRows,
            List<ContractCallTraceRow> contractCallRows,
            List<StorageWriteTraceRow> storageWriteRows,
            List<NotificationTraceRow> notificationRows,
            int batchSize,
            bool trimStaleRows,
            StateRecorderSettings settings)
        {
            await using var connection = new NpgsqlConnection(settings.SupabaseConnectionString);
            await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

            await UpsertTraceRowsPostgresAsync(
                connection,
                transaction,
                opCodeRows,
                syscallRows,
                contractCallRows,
                storageWriteRows,
                notificationRows,
                batchSize).ConfigureAwait(false);

            if (trimStaleRows)
            {
                await TryTrimTraceRowsPostgresAsync(
                    connection,
                    transaction,
                    blockIndex,
                    txHash,
                    opCodeRows.Count,
                    syscallRows.Count,
                    contractCallRows.Count,
                    storageWriteRows.Count,
                    notificationRows.Count).ConfigureAwait(false);
            }

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"PostgreSQL trace upload successful for tx {txHash} @ block {blockIndex}: opcode={opCodeRows.Count}, syscall={syscallRows.Count}, calls={contractCallRows.Count}, writes={storageWriteRows.Count}, notifications={notificationRows.Count}");
        }
#endif
    }
}

