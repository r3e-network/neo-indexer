// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Traces.TrimAll.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
#endif

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
#if NET9_0_OR_GREATER
        private static async Task TryTrimTraceRowsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int blockIndex,
            string txHash,
            int opCodeCount,
            int syscallCount,
            int contractCallCount,
            int storageWriteCount,
            int notificationCount,
            int runtimeLogCount)
        {
            try
            {
                await TrimTraceTailPostgresAsync(connection, transaction, "opcode_traces", "trace_order", blockIndex, txHash, opCodeCount).ConfigureAwait(false);
                await TrimTraceTailPostgresAsync(connection, transaction, "syscall_traces", "trace_order", blockIndex, txHash, syscallCount).ConfigureAwait(false);
                await TrimTraceTailPostgresAsync(connection, transaction, "contract_calls", "trace_order", blockIndex, txHash, contractCallCount).ConfigureAwait(false);
                await TrimTraceTailPostgresAsync(connection, transaction, "storage_writes", "write_order", blockIndex, txHash, storageWriteCount).ConfigureAwait(false);
                await TrimTraceTailPostgresAsync(connection, transaction, "notifications", "notification_order", blockIndex, txHash, notificationCount).ConfigureAwait(false);
                await TrimTraceTailPostgresAsync(connection, transaction, "runtime_logs", "log_order", blockIndex, txHash, runtimeLogCount).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                    $"PostgreSQL trace trim failed for tx {txHash} @ block {blockIndex}: {ex.Message}");
            }
        }
#endif
    }
}
