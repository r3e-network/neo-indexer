// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.BlockStats.Rows.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
#if NET9_0_OR_GREATER
        private static readonly string[] BlockStatsColumns =
        {
            "block_index",
            "tx_count",
            "total_gas_consumed",
            "opcode_count",
            "syscall_count",
            "contract_call_count",
            "storage_read_count",
            "storage_write_count",
            "notification_count"
        };

        private static object?[] BuildBlockStatsRow(BlockStats stats)
        {
            return new object?[]
            {
                checked((int)stats.BlockIndex),
                stats.TransactionCount,
                stats.TotalGasConsumed,
                stats.OpCodeCount,
                stats.SyscallCount,
                stats.ContractCallCount,
                stats.StorageReadCount,
                stats.StorageWriteCount,
                stats.NotificationCount
            };
        }
#endif
    }
}

