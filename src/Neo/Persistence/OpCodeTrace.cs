// Copyright (C) 2015-2025 The Neo Project.
//
// OpCodeTrace.cs file belongs to the neo project and is free
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
    public readonly record struct OpCodeTrace
    {
        /// <summary>
        /// The contract hash executing this instruction.
        /// </summary>
        public UInt160 ContractHash { get; init; }

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
        /// GAS consumed by this instruction (in datoshi).
        /// </summary>
        public long GasConsumed { get; init; }

        /// <summary>
        /// Evaluation stack depth before this instruction.
        /// </summary>
        public int StackDepth { get; init; }

        /// <summary>
        /// Execution order within the transaction.
        /// </summary>
        public int Order { get; init; }
    }
}

