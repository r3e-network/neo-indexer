// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexerPlugin.Handlers.Committed.Stats.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System.Collections.Generic;

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class BlockStateIndexerPlugin
    {
        private static BlockStats BuildBlockStats(Block block, IReadOnlyCollection<ExecutionTraceRecorder> recorders, int storageReadCount)
        {
            long totalGasConsumed = 0;
            int opCodeCount = 0;
            int syscallCount = 0;
            int contractCallCount = 0;
            int storageWriteCount = 0;
            int notificationCount = 0;

            foreach (var recorder in recorders)
            {
                var txStats = recorder.GetStats();
                totalGasConsumed += txStats.TotalGasConsumed;
                opCodeCount += txStats.OpCodeCount;
                syscallCount += txStats.SyscallCount;
                contractCallCount += txStats.ContractCallCount;
                storageWriteCount += txStats.StorageWriteCount;
                notificationCount += txStats.NotificationCount;
            }

            return new BlockStats
            {
                BlockIndex = block.Index,
                TransactionCount = block.Transactions.Length,
                TotalGasConsumed = totalGasConsumed,
                OpCodeCount = opCodeCount,
                SyscallCount = syscallCount,
                ContractCallCount = contractCallCount,
                StorageReadCount = storageReadCount,
                StorageWriteCount = storageWriteCount,
                NotificationCount = notificationCount
            };
        }
    }
}
