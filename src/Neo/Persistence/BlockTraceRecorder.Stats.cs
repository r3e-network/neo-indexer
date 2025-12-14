// Copyright (C) 2015-2025 The Neo Project.
//
// BlockTraceRecorder.Stats.cs file belongs to the neo project and is free
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
    public sealed partial class BlockTraceRecorder
    {
        /// <summary>
        /// Gets aggregated statistics for the entire block.
        /// </summary>
        public BlockStats GetBlockStats()
        {
            long totalGas = 0;
            int opCodeCount = 0;
            int syscallCount = 0;
            int contractCallCount = 0;
            int storageWriteCount = 0;
            int notificationCount = 0;
            int logCount = 0;

            foreach (var recorder in _txRecorders.Values)
            {
                var txStats = recorder.GetStats();
                totalGas += txStats.TotalGasConsumed;
                opCodeCount += txStats.OpCodeCount;
                syscallCount += txStats.SyscallCount;
                contractCallCount += txStats.ContractCallCount;
                storageWriteCount += txStats.StorageWriteCount;
                notificationCount += txStats.NotificationCount;
                logCount += txStats.LogCount;
            }

            return new BlockStats
            {
                BlockIndex = BlockIndex,
                TransactionCount = _txRecorders.Count,
                TotalGasConsumed = totalGas,
                OpCodeCount = opCodeCount,
                SyscallCount = syscallCount,
                ContractCallCount = contractCallCount,
                StorageReadCount = 0, // Filled separately
                StorageWriteCount = storageWriteCount,
                NotificationCount = notificationCount,
                LogCount = logCount
            };
        }
    }
}
