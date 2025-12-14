// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceRecorder.Recording.OpCodes.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Threading;

namespace Neo.Persistence
{
    public sealed partial class ExecutionTraceRecorder
    {
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
    }
}

