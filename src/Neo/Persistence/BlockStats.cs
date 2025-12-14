// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStats.cs file belongs to the neo project and is free
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
    /// <summary>
    /// Aggregated statistics for a block.
    /// </summary>
    public sealed class BlockStats
    {
        /// <summary>
        /// The block index.
        /// </summary>
        public uint BlockIndex { get; init; }

        /// <summary>
        /// Number of transactions in the block.
        /// </summary>
        public int TransactionCount { get; init; }

        /// <summary>
        /// Total GAS consumed by all transactions (in datoshi).
        /// </summary>
        public long TotalGasConsumed { get; init; }

        /// <summary>
        /// Total number of OpCode executions.
        /// </summary>
        public int OpCodeCount { get; init; }

        /// <summary>
        /// Total number of syscall invocations.
        /// </summary>
        public int SyscallCount { get; init; }

        /// <summary>
        /// Total number of contract calls.
        /// </summary>
        public int ContractCallCount { get; init; }

        /// <summary>
        /// Total number of storage reads.
        /// </summary>
        public int StorageReadCount { get; init; }

        /// <summary>
        /// Total number of storage writes.
        /// </summary>
        public int StorageWriteCount { get; init; }

        /// <summary>
        /// Total number of notifications.
        /// </summary>
        public int NotificationCount { get; init; }

        /// <summary>
        /// Total number of runtime logs (System.Runtime.Log).
        /// </summary>
        public int LogCount { get; init; }
    }
}
