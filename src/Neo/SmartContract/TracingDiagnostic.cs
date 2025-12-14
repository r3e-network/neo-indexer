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
    public sealed partial class TracingDiagnostic : IDiagnostic
    {
        private readonly ExecutionTraceRecorder _recorder;
        private ApplicationEngine? _engine;
        private long _lastFeeConsumed;
        private PendingOpCodeData? _pendingOpCode;
        private readonly Stack<(ContractCallTrace Trace, long GasStart)> _callStack = new();
        private readonly Dictionary<UInt160, Dictionary<int, string?>> _methodNameCache = new();

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
            _pendingOpCode = null;
            _callStack.Clear();
            _methodNameCache.Clear();
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
                _pendingOpCode = null;
                return;
            }

            _recorder.TotalGasConsumed = engine.FeeConsumed;

            // If the engine terminates between PreExecuteInstruction and PostExecuteInstruction,
            // emit the last pending opcode trace using the current FeeConsumed.
            if (_pendingOpCode is { } pending)
            {
                var delta = engine.FeeConsumed - _lastFeeConsumed;
                _recorder.RecordOpCode(
                    pending.ContractHash,
                    pending.InstructionPointer,
                    pending.OpCode,
                    pending.Operand,
                    gasConsumed: delta < 0 ? 0 : delta,
                    pending.StackDepth);
                _lastFeeConsumed = engine.FeeConsumed;
                _pendingOpCode = null;
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
    }
}
