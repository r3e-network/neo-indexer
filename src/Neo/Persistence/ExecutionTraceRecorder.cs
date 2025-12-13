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
		    public sealed partial class ExecutionTraceRecorder
		    {
	        private static readonly ConcurrentDictionary<uint, string> SyscallHashStringCache = new();

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
	        private int _opCodeCount;
	        private int _syscallCount;
	        private int _contractCallCount;
	        private int _storageWriteCount;
	        private int _notificationCount;

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
	            Interlocked.Increment(ref _opCodeCount);
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
	                Interlocked.Increment(ref _opCodeCount);
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
	            Interlocked.Increment(ref _syscallCount);
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
                SyscallHash = GetSyscallHashString(syscallHash),
                SyscallName = syscallName,
                GasCost = gasCost,
                Order = Interlocked.Increment(ref _syscallOrder) - 1
            };

	            if (IsEnabled)
	            {
	                _syscallTraces.Enqueue(trace);
	                Interlocked.Increment(ref _syscallCount);
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
	            Interlocked.Increment(ref _contractCallCount);
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
	                Interlocked.Increment(ref _contractCallCount);
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
	            Interlocked.Increment(ref _storageWriteCount);
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
	                Interlocked.Increment(ref _storageWriteCount);
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
	            Interlocked.Increment(ref _notificationCount);
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
	                Interlocked.Increment(ref _notificationCount);
	            }

	            return trace;
	        }
	    }
	}
