// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngineProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.VM;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Neo.SmartContract
{
    /// <summary>
    /// Factory for creating <see cref="TracingApplicationEngine"/> instances that share the same tracing configuration.
    /// </summary>
    public sealed class TracingApplicationEngineProvider : IApplicationEngineProvider
    {
        private readonly Func<ExecutionTraceRecorder> _recorderFactory;
        private readonly ExecutionTraceLevel _traceLevel;
        private readonly ConcurrentDictionary<uint, BlockTraceRecorder> _blockRecorders = new();
        private readonly ConcurrentDictionary<uint, BlockReadRecorderScope> _readScopes = new();

        /// <summary>
        /// Initializes a new provider with an optional recorder factory and trace level.
        /// </summary>
        public TracingApplicationEngineProvider(
            Func<ExecutionTraceRecorder>? recorderFactory = null,
            ExecutionTraceLevel traceLevel = ExecutionTraceLevel.All)
        {
            _recorderFactory = recorderFactory ?? (() => new ExecutionTraceRecorder());
            _traceLevel = traceLevel;
        }

        /// <summary>
        /// Initializes a new provider using a fixed recorder instance.
        /// </summary>
        public TracingApplicationEngineProvider(
            ExecutionTraceRecorder recorder,
            ExecutionTraceLevel traceLevel = ExecutionTraceLevel.All)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));
            _recorderFactory = () => recorder;
            _traceLevel = traceLevel;
        }

        /// <summary>
        /// Creates a <see cref="TracingApplicationEngine"/> using the configured recorder and diagnostics.
        /// </summary>
        public ApplicationEngine Create(
            TriggerType trigger,
            IVerifiable container,
            DataCache snapshot,
            Block? persistingBlock,
            ProtocolSettings settings,
            long gas,
            IDiagnostic diagnostic,
            JumpTable jumpTable)
        {
            if (persistingBlock != null && trigger == TriggerType.OnPersist && StateReadRecorder.Enabled)
            {
                _readScopes.GetOrAdd(persistingBlock.Index, _ =>
                {
                    var scope = StateReadRecorder.TryBegin(persistingBlock);
                    return scope ?? throw new InvalidOperationException("StateReadRecorder.TryBegin returned null unexpectedly.");
                });
            }

            ExecutionTraceRecorder recorder;

            if (trigger == TriggerType.Application &&
                container is Transaction tx &&
                persistingBlock != null)
            {
                var blockRecorder = _blockRecorders.GetOrAdd(persistingBlock.Index, index =>
                {
                    var created = new BlockTraceRecorder(index)
                    {
                        BlockHash = persistingBlock.Hash,
                        Timestamp = persistingBlock.Timestamp
                    };
                    return created;
                });

                recorder = blockRecorder.GetOrCreateTxRecorder(tx.Hash);
                recorder.IsEnabled = _traceLevel != ExecutionTraceLevel.None;
            }
            else
            {
                recorder = _recorderFactory()
                    ?? throw new InvalidOperationException("The recorder factory returned null.");

                // For non-persisted executions (e.g., RPC invokes), honor trace level
                // when the trigger is Application. Other triggers remain disabled to
                // avoid noisy traces during system callbacks.
                recorder.IsEnabled = trigger == TriggerType.Application && _traceLevel != ExecutionTraceLevel.None;
                recorder.BlockIndex = persistingBlock?.Index ?? 0;
                if (container is Transaction nonPersistTx)
                    recorder.TxHash = nonPersistTx.Hash;
            }

            var (effectiveRecorder, effectiveDiagnostic) = ResolveDiagnosticAndRecorder(diagnostic, recorder);

            return new TracingApplicationEngine(
                trigger,
                container,
                snapshot,
                persistingBlock,
                settings,
                gas,
                effectiveRecorder,
                _traceLevel,
                effectiveDiagnostic,
                jumpTable);
        }

        /// <summary>
        /// Drains and returns all transaction recorders captured for a given block.
        /// </summary>
        public IReadOnlyCollection<ExecutionTraceRecorder> DrainBlock(uint blockIndex)
        {
            if (_blockRecorders.TryRemove(blockIndex, out var blockRecorder))
            {
                return blockRecorder.GetTxRecorders().Values.ToArray();
            }

            return Array.Empty<ExecutionTraceRecorder>();
        }

        /// <summary>
        /// Drains and returns the state read recorder for a block, if present.
        /// </summary>
        public BlockReadRecorder? DrainReadRecorder(uint blockIndex)
        {
            if (_readScopes.TryRemove(blockIndex, out var scope))
            {
                var recorder = scope.Recorder;
                scope.Dispose();
                return recorder;
            }

            return null;
        }

        private (ExecutionTraceRecorder Recorder, IDiagnostic Diagnostic) ResolveDiagnosticAndRecorder(
            IDiagnostic? requested,
            ExecutionTraceRecorder recorder)
        {
            if (requested is TracingDiagnostic tracingDiagnostic)
            {
                ConfigureDiagnostic(tracingDiagnostic);
                return (tracingDiagnostic.Recorder, tracingDiagnostic);
            }

            var tracing = new TracingDiagnostic(recorder);
            var readTxDiagnostic = new StateReadTransactionDiagnostic();
            ConfigureDiagnostic(tracing);

            if (requested is null)
                return (recorder, new DiagnosticCollection(tracing, readTxDiagnostic));

            return (recorder, new DiagnosticCollection(tracing, readTxDiagnostic, requested));
        }

        private void ConfigureDiagnostic(TracingDiagnostic diagnostic)
        {
            diagnostic.TraceOpCodes = (_traceLevel & ExecutionTraceLevel.OpCodes) != 0;
            diagnostic.TraceContractCalls = (_traceLevel & ExecutionTraceLevel.ContractCalls) != 0;
        }

        private sealed class DiagnosticCollection : IDiagnostic
        {
            private readonly IReadOnlyList<IDiagnostic> _diagnostics;

            public DiagnosticCollection(params IDiagnostic[] diagnostics)
            {
                _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            }

            public void Initialized(ApplicationEngine engine)
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.Initialized(engine);
            }

            public void Disposed()
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.Disposed();
            }

            public void ContextLoaded(ExecutionContext context)
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.ContextLoaded(context);
            }

            public void ContextUnloaded(ExecutionContext context)
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.ContextUnloaded(context);
            }

            public void PreExecuteInstruction(Instruction instruction)
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.PreExecuteInstruction(instruction);
            }

            public void PostExecuteInstruction(Instruction instruction)
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.PostExecuteInstruction(instruction);
            }
        }

        private sealed class StateReadTransactionDiagnostic : IDiagnostic
        {
            private IDisposable? _scope;

            public void Initialized(ApplicationEngine engine)
            {
                if (engine.ScriptContainer is Transaction tx)
                {
                    _scope = StateReadRecorder.BeginTransaction(tx.Hash);
                }
            }

            public void Disposed()
            {
                _scope?.Dispose();
                _scope = null;
            }

            public void ContextLoaded(ExecutionContext context) { }
            public void ContextUnloaded(ExecutionContext context) { }
            public void PreExecuteInstruction(Instruction instruction) { }
            public void PostExecuteInstruction(Instruction instruction) { }
        }
    }
}
