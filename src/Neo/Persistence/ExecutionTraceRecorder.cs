// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceRecorder.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Neo.Persistence
{
    /// <summary>
    /// Aggregates execution trace data for a single transaction.
    /// Thread-safe for concurrent recording from multiple sources.
    /// </summary>
    public sealed class ExecutionTraceRecorder
    {
        private readonly ConcurrentQueue<OpCodeTrace> _opCodeTraces = new();
        private readonly ConcurrentQueue<SyscallTrace> _syscallTraces = new();
        private readonly ConcurrentQueue<ContractCallTrace> _contractCalls = new();
        private readonly ConcurrentQueue<StorageWriteTrace> _storageWrites = new();
        private readonly ConcurrentQueue<NotificationTrace> _notifications = new();

        private int _opCodeOrder;
        private int _syscallOrder;
        private int _contractCallOrder;
        private int _storageWriteOrder;
        private int _notificationOrder;

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
        /// Whether tracing is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Records an OpCode execution trace.
        /// </summary>
        public void RecordOpCode(OpCodeTrace trace)
        {
            if (!IsEnabled) return;
            _opCodeTraces.Enqueue(trace);
        }

        /// <summary>
        /// Creates and records an OpCode trace with auto-incrementing order.
        /// </summary>
        public OpCodeTrace RecordOpCode(
            UInt160 contractHash,
            int instructionPointer,
            VM.OpCode opCode,
            ReadOnlyMemory<byte> operand,
            long gasConsumed,
            int stackDepth)
        {
            var trace = new OpCodeTrace
            {
                ContractHash = contractHash,
                InstructionPointer = instructionPointer,
                OpCode = opCode,
                Operand = operand,
                GasConsumed = gasConsumed,
                StackDepth = stackDepth,
                Order = Interlocked.Increment(ref _opCodeOrder) - 1
            };

            if (IsEnabled)
            {
                _opCodeTraces.Enqueue(trace);
            }

            return trace;
        }

        /// <summary>
        /// Records a syscall invocation trace.
        /// </summary>
        public void RecordSyscall(SyscallTrace trace)
        {
            if (!IsEnabled) return;
            _syscallTraces.Enqueue(trace);
        }

        /// <summary>
        /// Creates and records a syscall trace with auto-incrementing order.
        /// </summary>
        public SyscallTrace RecordSyscall(
            UInt160 contractHash,
            uint syscallHash,
            string syscallName,
            long gasCost)
        {
            var trace = new SyscallTrace
            {
                ContractHash = contractHash,
                SyscallHash = syscallHash.ToString("X8"),
                SyscallName = syscallName,
                GasCost = gasCost,
                Order = Interlocked.Increment(ref _syscallOrder) - 1
            };

            if (IsEnabled)
            {
                _syscallTraces.Enqueue(trace);
            }

            return trace;
        }

        /// <summary>
        /// Records a contract call trace.
        /// </summary>
        public void RecordContractCall(ContractCallTrace trace)
        {
            if (!IsEnabled) return;
            _contractCalls.Enqueue(trace);
        }

        /// <summary>
        /// Creates and records a contract call trace with auto-incrementing order.
        /// </summary>
        public ContractCallTrace RecordContractCall(
            UInt160? callerHash,
            UInt160 calleeHash,
            string? methodName,
            int callDepth)
        {
            var trace = new ContractCallTrace
            {
                CallerHash = callerHash,
                CalleeHash = calleeHash,
                MethodName = methodName,
                CallDepth = callDepth,
                Order = Interlocked.Increment(ref _contractCallOrder) - 1
            };

            if (IsEnabled)
            {
                _contractCalls.Enqueue(trace);
            }

            return trace;
        }

        /// <summary>
        /// Records a storage write trace.
        /// </summary>
        public void RecordStorageWrite(StorageWriteTrace trace)
        {
            if (!IsEnabled) return;
            _storageWrites.Enqueue(trace);
        }

        /// <summary>
        /// Creates and records a storage write trace with auto-incrementing order.
        /// </summary>
        public StorageWriteTrace RecordStorageWrite(
            int contractId,
            UInt160 contractHash,
            ReadOnlyMemory<byte> key,
            ReadOnlyMemory<byte>? oldValue,
            ReadOnlyMemory<byte> newValue)
        {
            var trace = new StorageWriteTrace
            {
                ContractId = contractId,
                ContractHash = contractHash,
                Key = key,
                OldValue = oldValue,
                NewValue = newValue,
                Order = Interlocked.Increment(ref _storageWriteOrder) - 1
            };

            if (IsEnabled)
            {
                _storageWrites.Enqueue(trace);
            }

            return trace;
        }

        /// <summary>
        /// Records a notification trace.
        /// </summary>
        public void RecordNotification(NotificationTrace trace)
        {
            if (!IsEnabled) return;
            _notifications.Enqueue(trace);
        }

        /// <summary>
        /// Creates and records a notification trace with auto-incrementing order.
        /// </summary>
        public NotificationTrace RecordNotification(
            UInt160 contractHash,
            string eventName,
            string? stateJson)
        {
            var trace = new NotificationTrace
            {
                ContractHash = contractHash,
                EventName = eventName,
                StateJson = stateJson,
                Order = Interlocked.Increment(ref _notificationOrder) - 1
            };

            if (IsEnabled)
            {
                _notifications.Enqueue(trace);
            }

            return trace;
        }

        /// <summary>
        /// Gets all recorded OpCode traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<OpCodeTrace> GetOpCodeTraces()
        {
            return _opCodeTraces.OrderBy(t => t.Order).ToList();
        }

        /// <summary>
        /// Gets all recorded syscall traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<SyscallTrace> GetSyscallTraces()
        {
            return _syscallTraces.OrderBy(t => t.Order).ToList();
        }

        /// <summary>
        /// Gets all recorded contract call traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<ContractCallTrace> GetContractCallTraces()
        {
            return _contractCalls.OrderBy(t => t.Order).ToList();
        }

        /// <summary>
        /// Gets all recorded storage write traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<StorageWriteTrace> GetStorageWriteTraces()
        {
            return _storageWrites.OrderBy(t => t.Order).ToList();
        }

        /// <summary>
        /// Gets all recorded notification traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<NotificationTrace> GetNotificationTraces()
        {
            return _notifications.OrderBy(t => t.Order).ToList();
        }

        /// <summary>
        /// Gets aggregated statistics for this transaction.
        /// </summary>
        public BlockStats GetStats()
        {
            var opCodeTraces = GetOpCodeTraces();
            long resolvedTotalGasConsumed;
            if (TotalGasConsumed.HasValue)
            {
                resolvedTotalGasConsumed = TotalGasConsumed.Value;
            }
            else if (opCodeTraces.Count == 0)
            {
                resolvedTotalGasConsumed = 0;
            }
            else
            {
                bool looksCumulative = true;
                long previous = opCodeTraces[0].GasConsumed;
                for (int i = 1; i < opCodeTraces.Count; i++)
                {
                    var current = opCodeTraces[i].GasConsumed;
                    if (current < previous)
                    {
                        looksCumulative = false;
                        break;
                    }
                    previous = current;
                }

                resolvedTotalGasConsumed = looksCumulative
                    ? opCodeTraces[^1].GasConsumed
                    : opCodeTraces.Sum(t => t.GasConsumed);
            }

            return new BlockStats
            {
                BlockIndex = BlockIndex,
                TransactionCount = 1,
                TotalGasConsumed = resolvedTotalGasConsumed,
                OpCodeCount = opCodeTraces.Count,
                SyscallCount = _syscallTraces.Count,
                ContractCallCount = _contractCalls.Count,
                StorageReadCount = 0, // Filled by BlockReadRecorder
                StorageWriteCount = _storageWrites.Count,
                NotificationCount = _notifications.Count
            };
        }

        /// <summary>
        /// Clears all recorded traces.
        /// </summary>
        public void Clear()
        {
            while (_opCodeTraces.TryDequeue(out _)) { }
            while (_syscallTraces.TryDequeue(out _)) { }
            while (_contractCalls.TryDequeue(out _)) { }
            while (_storageWrites.TryDequeue(out _)) { }
            while (_notifications.TryDequeue(out _)) { }

            _opCodeOrder = 0;
            _syscallOrder = 0;
            _contractCallOrder = 0;
            _storageWriteOrder = 0;
            _notificationOrder = 0;
        }

        /// <summary>
        /// Returns true if any traces have been recorded.
        /// </summary>
        public bool HasTraces =>
            !_opCodeTraces.IsEmpty ||
            !_syscallTraces.IsEmpty ||
            !_contractCalls.IsEmpty ||
            !_storageWrites.IsEmpty ||
            !_notifications.IsEmpty;
    }

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
            var stats = new BlockStats
            {
                BlockIndex = BlockIndex,
                TransactionCount = _txRecorders.Count
            };

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
