// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceRecorder.Core.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Concurrent;
using Neo.VM;

namespace Neo.Persistence
{
    /// <summary>
    /// Aggregates execution trace data for a single transaction.
    /// Thread-safe for concurrent recording from multiple sources.
    /// </summary>
    public sealed partial class ExecutionTraceRecorder
    {
        private static readonly ConcurrentDictionary<uint, string> SyscallHashStringCache = new();

        private readonly ConcurrentQueue<OpCodeTrace> _opCodeTraces = new();
        private readonly ConcurrentQueue<SyscallTrace> _syscallTraces = new();
        private readonly ConcurrentQueue<ContractCallTrace> _contractCalls = new();
        private readonly ConcurrentQueue<StorageWriteTrace> _storageWrites = new();
        private readonly ConcurrentQueue<NotificationTrace> _notifications = new();
        private readonly ConcurrentQueue<LogTrace> _logs = new();

        private int _opCodeOrder;
        private int _syscallOrder;
        private int _contractCallOrder;
        private int _storageWriteOrder;
        private int _notificationOrder;
        private int _logOrder;
        private int _opCodeCount;
        private int _syscallCount;
        private int _contractCallCount;
        private int _storageWriteCount;
        private int _notificationCount;
        private int _logCount;

        private static string GetSyscallHashString(uint syscallHash)
        {
            if (SyscallHashStringCache.TryGetValue(syscallHash, out var cached))
                return cached;

            var formatted = syscallHash.ToString("X8");
            return SyscallHashStringCache.GetOrAdd(syscallHash, formatted);
        }

        /// <summary>
        /// The block index this recorder is associated with.
        /// </summary>
        public uint BlockIndex { get; set; }

        /// <summary>
        /// The transaction hash this recorder is associated with.
        /// </summary>
        public UInt256? TxHash { get; set; }

        /// <summary>
        /// Total GAS consumed by the transaction (in datoshi), captured from the engine at completion when available.
        /// </summary>
        public long? TotalGasConsumed { get; set; }

        /// <summary>
        /// Final VM state for this transaction execution (HALT/FAULT).
        /// </summary>
        public VMState VmState { get; set; } = VMState.NONE;

        /// <summary>
        /// Fault exception details when <see cref="VmState"/> is FAULT.
        /// </summary>
        public string? FaultException { get; set; }

        /// <summary>
        /// Final result stack serialized as JSON (best effort).
        /// </summary>
        public string? ResultStackJson { get; set; }

        /// <summary>
        /// Whether tracing is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }
}
