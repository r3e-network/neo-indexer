// Copyright (C) 2015-2025 The Neo Project.
//
// TracingDiagnostic.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;
using Neo.VM;
using System;
using System.Collections.Generic;

namespace Neo.SmartContract
{
    /// <summary>
    /// IDiagnostic implementation that captures OpCode execution traces
    /// and contract call graph information.
    /// </summary>
    public sealed class TracingDiagnostic : IDiagnostic
    {
        private readonly ExecutionTraceRecorder _recorder;
        private ApplicationEngine? _engine;
        private long _lastGasConsumed;
        private readonly Stack<(ContractCallTrace Trace, long GasStart)> _callStack = new();

        /// <summary>
        /// Gets the trace recorder associated with this diagnostic.
        /// </summary>
        public ExecutionTraceRecorder Recorder => _recorder;

        /// <summary>
        /// Whether OpCode tracing is enabled.
        /// </summary>
        public bool TraceOpCodes { get; set; } = true;

        /// <summary>
        /// Whether contract call tracing is enabled.
        /// </summary>
        public bool TraceContractCalls { get; set; } = true;

        /// <summary>
        /// Creates a new TracingDiagnostic with the specified recorder.
        /// </summary>
        public TracingDiagnostic(ExecutionTraceRecorder recorder)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        /// <summary>
        /// Called when the ApplicationEngine is initialized.
        /// </summary>
        public void Initialized(ApplicationEngine engine)
        {
            _engine = engine;
            _lastGasConsumed = 0;
            _callStack.Clear();
        }

        /// <summary>
        /// Called when the ApplicationEngine is disposed.
        /// </summary>
        public void Disposed()
        {
            // Mark any remaining calls as completed (abnormal termination)
            while (_callStack.Count > 0)
            {
                _callStack.Pop();
            }

            _engine = null;
        }

        /// <summary>
        /// Called when a new execution context is loaded (contract call starts).
        /// </summary>
        public void ContextLoaded(ExecutionContext context)
        {
            if (!TraceContractCalls || _engine == null) return;

            var calleeHash = context.GetScriptHash();
            var callerHash = _engine.CallingScriptHash;
            var callDepth = _engine.InvocationStack.Count;

            // Record the contract call
            var trace = _recorder.RecordContractCall(
                callerHash,
                calleeHash,
                methodName: null, // Method name not easily available here
                callDepth);

            // Push to call stack for tracking completion
            _callStack.Push((trace, _engine.FeeConsumed));
        }

        /// <summary>
        /// Called when an execution context is unloaded (contract call ends).
        /// </summary>
        public void ContextUnloaded(ExecutionContext context)
        {
            if (!TraceContractCalls || _engine == null) return;

            if (_callStack.Count > 0)
            {
                var (trace, gasStart) = _callStack.Pop();
                var gasConsumed = _engine.FeeConsumed - gasStart;

                trace.GasConsumed = gasConsumed;
                if (_engine.State == VMState.FAULT || _engine.FaultException is not null)
                    trace.Success = false;
            }
        }

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

            _recorder.RecordOpCode(
                contractHash,
                instructionPointer,
                instruction.OpCode,
                instruction.Operand,
                _engine.FeeConsumed,
                stackDepth);

            _lastGasConsumed = _engine.FeeConsumed;
        }

        /// <summary>
        /// Called after each instruction is executed.
        /// </summary>
        public void PostExecuteInstruction(Instruction instruction)
        {
            // GAS consumed by this instruction = current - last
            // This could be used to update the trace with actual GAS cost
            // For now, we record the cumulative GAS in PreExecuteInstruction
        }
    }
}
