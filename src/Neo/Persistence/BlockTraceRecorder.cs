// Copyright (C) 2015-2025 The Neo Project.
//
// BlockTraceRecorder.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Neo.Persistence
{
    /// <summary>
    /// Aggregates execution traces for an entire block.
    /// </summary>
    public sealed class BlockTraceRecorder
    {
        private readonly ConcurrentDictionary<UInt256, ExecutionTraceRecorder> _txRecorders = new();

        /// <summary>
        /// The block index.
        /// </summary>
        public uint BlockIndex { get; }

        /// <summary>
        /// The block hash.
        /// </summary>
        public UInt256 BlockHash { get; set; } = UInt256.Zero;

        /// <summary>
        /// The block timestamp.
        /// </summary>
        public ulong Timestamp { get; set; }

        public BlockTraceRecorder(uint blockIndex)
        {
            BlockIndex = blockIndex;
        }

        /// <summary>
        /// Gets or creates a recorder for the specified transaction.
        /// </summary>
        public ExecutionTraceRecorder GetOrCreateTxRecorder(UInt256 txHash)
        {
            return _txRecorders.GetOrAdd(txHash, hash => new ExecutionTraceRecorder
            {
                BlockIndex = BlockIndex,
                TxHash = hash
            });
        }

        /// <summary>
        /// Gets all transaction recorders.
        /// </summary>
        public IReadOnlyDictionary<UInt256, ExecutionTraceRecorder> GetTxRecorders()
        {
            return _txRecorders;
        }

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

            foreach (var recorder in _txRecorders.Values)
            {
                var txStats = recorder.GetStats();
                totalGas += txStats.TotalGasConsumed;
                opCodeCount += txStats.OpCodeCount;
                syscallCount += txStats.SyscallCount;
                contractCallCount += txStats.ContractCallCount;
                storageWriteCount += txStats.StorageWriteCount;
                notificationCount += txStats.NotificationCount;
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
                NotificationCount = notificationCount
            };
        }

        /// <summary>
        /// Clears all recorded traces.
        /// </summary>
        public void Clear()
        {
            foreach (var recorder in _txRecorders.Values)
            {
                recorder.Clear();
            }
            _txRecorders.Clear();
        }
    }
}

