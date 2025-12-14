// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Traces.UpsertAll.cs file belongs to the neo project and is free
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
        private static async Task UpsertTraceRowsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            List<OpCodeTraceRow> opCodeRows,
            List<SyscallTraceRow> syscallRows,
            List<ContractCallTraceRow> contractCallRows,
            List<StorageWriteTraceRow> storageWriteRows,
            List<NotificationTraceRow> notificationRows,
            List<RuntimeLogTraceRow> runtimeLogRows,
            int batchSize)
        {
            if (opCodeRows.Count > 0)
                await UpsertOpCodeTracesPostgresAsync(connection, transaction, opCodeRows, batchSize).ConfigureAwait(false);

            if (syscallRows.Count > 0)
                await UpsertSyscallTracesPostgresAsync(connection, transaction, syscallRows, batchSize).ConfigureAwait(false);

            if (contractCallRows.Count > 0)
                await UpsertContractCallTracesPostgresAsync(connection, transaction, contractCallRows, batchSize).ConfigureAwait(false);

            if (storageWriteRows.Count > 0)
                await UpsertStorageWriteTracesPostgresAsync(connection, transaction, storageWriteRows, batchSize).ConfigureAwait(false);

            if (notificationRows.Count > 0)
                await UpsertNotificationTracesPostgresAsync(connection, transaction, notificationRows, batchSize).ConfigureAwait(false);

            if (runtimeLogRows.Count > 0)
                await UpsertRuntimeLogTracesPostgresAsync(connection, transaction, runtimeLogRows, batchSize).ConfigureAwait(false);
        }
#endif
    }
}
