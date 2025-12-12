// Copyright (C) 2015-2025 The Neo Project.
//
// TraceModels.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.VM;
using System;

namespace Neo.Persistence
{
    /// <summary>
    /// Represents a single OpCode execution trace entry.
    /// Captured via IDiagnostic.PreExecuteInstruction/PostExecuteInstruction.
    /// </summary>
    public sealed class OpCodeTrace
    {
        /// <summary>
        /// The contract hash executing this instruction.
        /// </summary>
        public UInt160 ContractHash { get; init; } = UInt160.Zero;

        /// <summary>
        /// The instruction pointer (offset in script).
        /// </summary>
        public int InstructionPointer { get; init; }

        /// <summary>
        /// The OpCode being executed.
        /// </summary>
        public OpCode OpCode { get; init; }

        /// <summary>
        /// Human-readable OpCode name.
        /// </summary>
        public string OpCodeName => OpCode.ToString();

        /// <summary>
        /// The operand bytes (if any).
        /// </summary>
        public ReadOnlyMemory<byte> Operand { get; init; }

        /// <summary>
        /// GAS consumed up to and including this instruction (in datoshi).
        /// </summary>
        public long GasConsumed { get; set; }

        /// <summary>
        /// Evaluation stack depth before this instruction.
        /// </summary>
        public int StackDepth { get; init; }

        /// <summary>
        /// Execution order within the transaction.
        /// </summary>
        public int Order { get; init; }
    }

    /// <summary>
    /// Represents a syscall invocation trace entry.
    /// Captured via ApplicationEngine.OnSysCall override.
    /// </summary>
    public sealed class SyscallTrace
    {
        /// <summary>
        /// The contract hash invoking this syscall.
        /// </summary>
        public UInt160 ContractHash { get; init; } = UInt160.Zero;

        /// <summary>
        /// The syscall hash (uint32 as hex string).
        /// </summary>
        public string SyscallHash { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable syscall name (e.g., "System.Storage.Get").
        /// </summary>
        public string SyscallName { get; init; } = string.Empty;

        /// <summary>
        /// GAS cost of this syscall (in datoshi).
        /// </summary>
        public long GasCost { get; init; }

        /// <summary>
        /// Execution order within the transaction.
        /// </summary>
        public int Order { get; init; }
    }

    /// <summary>
    /// Represents a contract-to-contract call trace entry.
    /// Captured via IDiagnostic.ContextLoaded/ContextUnloaded.
    /// </summary>
    public sealed class ContractCallTrace
    {
        /// <summary>
        /// The calling contract hash (null for entry point).
        /// </summary>
        public UInt160? CallerHash { get; init; }

        /// <summary>
        /// The called contract hash.
        /// </summary>
        public UInt160 CalleeHash { get; init; } = UInt160.Zero;

        /// <summary>
        /// The method name being called (if available).
        /// </summary>
        public string? MethodName { get; init; }

        /// <summary>
        /// Call stack depth (1 = entry point).
        /// </summary>
        public int CallDepth { get; init; }

        /// <summary>
        /// Execution order within the transaction.
        /// </summary>
        public int Order { get; init; }

        /// <summary>
        /// Whether the call completed successfully.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// GAS consumed by this call (set on completion).
        /// </summary>
        public long GasConsumed { get; set; }
    }

    /// <summary>
    /// Represents a storage write operation trace entry.
    /// </summary>
    public sealed class StorageWriteTrace
    {
        /// <summary>
        /// The contract ID performing the write.
        /// </summary>
        public int ContractId { get; init; }

        /// <summary>
        /// The contract hash performing the write.
        /// </summary>
        public UInt160 ContractHash { get; init; } = UInt160.Zero;

        /// <summary>
        /// The storage key being written.
        /// </summary>
        public ReadOnlyMemory<byte> Key { get; init; }

        /// <summary>
        /// The old value (null if new key).
        /// </summary>
        public ReadOnlyMemory<byte>? OldValue { get; init; }

        /// <summary>
        /// The new value being written.
        /// </summary>
        public ReadOnlyMemory<byte> NewValue { get; init; }

        /// <summary>
        /// Write order within the transaction.
        /// </summary>
        public int Order { get; init; }
    }

    /// <summary>
    /// Represents a notification event trace entry.
    /// </summary>
    public sealed class NotificationTrace
    {
        /// <summary>
        /// The contract hash emitting the notification.
        /// </summary>
        public UInt160 ContractHash { get; init; } = UInt160.Zero;

        /// <summary>
        /// The event name.
        /// </summary>
        public string EventName { get; init; } = string.Empty;

        /// <summary>
        /// The notification state as JSON.
        /// </summary>
        public string? StateJson { get; init; }

        /// <summary>
        /// Notification order within the transaction.
        /// </summary>
        public int Order { get; init; }
    }

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
    }
}
