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
using Neo.SmartContract.Manifest;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;

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
        private long _lastFeeConsumed;
        private OpCodeTrace? _lastOpCodeTrace;
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
            _lastFeeConsumed = engine.FeeConsumed;
            _lastOpCodeTrace = null;
            _callStack.Clear();
        }

        /// <summary>
        /// Called when the ApplicationEngine is disposed.
        /// </summary>
        public void Disposed()
        {
            var engine = _engine;
            if (engine is null)
            {
                _callStack.Clear();
                _lastOpCodeTrace = null;
                return;
            }

            _recorder.TotalGasConsumed = engine.FeeConsumed;

            // If the engine terminates between PreExecuteInstruction and PostExecuteInstruction,
            // finalize the last pending opcode trace using the current FeeConsumed.
            if (_lastOpCodeTrace is not null)
            {
                var delta = engine.FeeConsumed - _lastFeeConsumed;
                _lastOpCodeTrace.GasConsumed = delta < 0 ? 0 : delta;
                _lastFeeConsumed = engine.FeeConsumed;
                _lastOpCodeTrace = null;
            }

            var faulted = engine.State == VMState.FAULT || engine.FaultException is not null;

            // Mark any remaining calls as completed (abnormal termination)
            while (_callStack.Count > 0)
            {
                var (trace, gasStart) = _callStack.Pop();
                var gasConsumed = engine.FeeConsumed - gasStart;
                trace.GasConsumed = gasConsumed < 0 ? 0 : gasConsumed;
                if (faulted)
                    trace.Success = false;
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

            string? methodName = null;
            try
            {
                var state = context.GetState<ExecutionContextState>();
                var contractState = state.Contract;
                var manifest = contractState?.Manifest;
                var offset = context.InstructionPointer;

                ContractMethodDescriptor? descriptor = manifest?.Abi?.Methods?.FirstOrDefault(m => m.Offset == offset);
                methodName = descriptor?.Name;
            }
            catch
            {
                methodName = null;
            }

            // Record the contract call
            var trace = _recorder.RecordContractCall(
                callerHash,
                calleeHash,
                methodName: methodName,
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

            _lastFeeConsumed = _engine.FeeConsumed;
            _lastOpCodeTrace = _recorder.RecordOpCode(
                contractHash,
                instructionPointer,
                instruction.OpCode,
                instruction.Operand,
                gasConsumed: 0,
                stackDepth);
        }

        /// <summary>
        /// Called after each instruction is executed.
        /// </summary>
        public void PostExecuteInstruction(Instruction instruction)
        {
            if (!TraceOpCodes || _engine == null) return;

            var trace = _lastOpCodeTrace;
            if (trace is null) return;

            var delta = _engine.FeeConsumed - _lastFeeConsumed;
            trace.GasConsumed = delta < 0 ? 0 : delta;
            _lastFeeConsumed = _engine.FeeConsumed;
            _lastOpCodeTrace = null;
        }
    }
}
