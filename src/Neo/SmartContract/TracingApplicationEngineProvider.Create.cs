// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngineProvider.Create.cs file belongs to the neo project and is free
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

namespace Neo.SmartContract
{
    public sealed partial class TracingApplicationEngineProvider
    {
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
    }
}

