// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngine.Diagnostic.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;

namespace Neo.UnitTests.SmartContract
{
    public partial class UT_TracingApplicationEngine
    {
        [TestMethod]
        public void TracingDiagnostic_RecordsOpCodes()
        {
            var recorder = new ExecutionTraceRecorder { IsEnabled = true };
            var diagnostic = new TracingDiagnostic(recorder) { TraceOpCodes = true };
            var provider = new TracingApplicationEngineProvider(() => recorder, ExecutionTraceLevel.OpCodes);

            using var engine = (TracingApplicationEngine)provider.Create(
                TriggerType.Application,
                null,
                _snapshotCache.CloneCache(),
                _persistingBlock,
                TestProtocolSettings.Default,
                ApplicationEngine.TestModeGas,
                diagnostic,
                null);

            UInt160 scriptHash = UInt160.Parse("0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
            engine.LoadScript(new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.PUSH2, (byte)OpCode.ADD, (byte)OpCode.RET },
                configureState: state => state.ScriptHash = scriptHash);
            engine.Execute();

            var traces = recorder.GetOpCodeTraces();
            Assert.IsTrue(traces.Count >= 4, $"Expected at least 4 opcode traces, got {traces.Count}");
        }

        [TestMethod]
        public void TracingDiagnostic_Recorder_Property()
        {
            var recorder = new ExecutionTraceRecorder();
            var diagnostic = new TracingDiagnostic(recorder);

            Assert.AreSame(recorder, diagnostic.Recorder);
        }

        [TestMethod]
        public void TracingDiagnostic_TraceFlags_Default()
        {
            var recorder = new ExecutionTraceRecorder();
            var diagnostic = new TracingDiagnostic(recorder);

            // Default values are true as per TracingDiagnostic.cs implementation
            Assert.IsTrue(diagnostic.TraceOpCodes);
            Assert.IsTrue(diagnostic.TraceContractCalls);
        }
    }
}

