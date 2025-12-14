// Copyright (C) 2015-2025 The Neo Project.
//
// TracingDiagnostic.OpCodes.cs file belongs to the neo project and is free
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

namespace Neo.SmartContract
{
	public sealed partial class TracingDiagnostic
	{
	    /// <summary>
	    /// Called before each instruction is executed.
	    /// </summary>
	    public void PreExecuteInstruction(Instruction instruction)
	    {
	        if (!TraceOpCodes || _engine == null) return;

	        var currentContext = _engine.CurrentContext;
	        if (currentContext == null) return;

	        var contractHash = currentContext.GetScriptHash();
	        var instructionPointer = currentContext.InstructionPointer;
	        var stackDepth = currentContext.EvaluationStack.Count;

	        _lastFeeConsumed = _engine.FeeConsumed;
	        _pendingOpCode = new PendingOpCodeData(
	            contractHash,
	            instructionPointer,
	            instruction.OpCode,
	            instruction.Operand,
	            stackDepth);
	    }

	    /// <summary>
	    /// Called after each instruction is executed.
	    /// </summary>
	    public void PostExecuteInstruction(Instruction instruction)
	    {
	        if (!TraceOpCodes || _engine == null) return;

	        if (_pendingOpCode is not { } pending) return;

	        var delta = _engine.FeeConsumed - _lastFeeConsumed;
	        _recorder.RecordOpCode(
	            pending.ContractHash,
	            pending.InstructionPointer,
	            pending.OpCode,
	            pending.Operand,
	            gasConsumed: delta < 0 ? 0 : delta,
	            pending.StackDepth);
	        _lastFeeConsumed = _engine.FeeConsumed;
	        _pendingOpCode = null;
	    }

	    private readonly record struct PendingOpCodeData(
	        UInt160 ContractHash,
	        int InstructionPointer,
	        OpCode OpCode,
	        ReadOnlyMemory<byte> Operand,
	        int StackDepth);
	}
}

