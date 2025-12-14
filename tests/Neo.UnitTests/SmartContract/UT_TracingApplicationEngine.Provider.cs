// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngine.Provider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using System.Linq;

namespace Neo.UnitTests.SmartContract
{
    public partial class UT_TracingApplicationEngine
    {
        [TestMethod]
        public void TracingApplicationEngineProvider_Create_ReturnsTracingEngine()
        {
            var provider = new TracingApplicationEngineProvider();

            var engine = provider.Create(
                TriggerType.Application,
                null,
                _snapshotCache.CloneCache(),
                _persistingBlock,
                TestProtocolSettings.Default,
                ApplicationEngine.TestModeGas,
                null,
                null);

            Assert.IsInstanceOfType(engine, typeof(TracingApplicationEngine));
            engine.Dispose();
        }

        [TestMethod]
        public void TracingApplicationEngineProvider_UsesRecorderFactory()
        {
            var recorder = new ExecutionTraceRecorder();
            var provider = new TracingApplicationEngineProvider(() => recorder, ExecutionTraceLevel.Syscalls);

            var engine = provider.Create(
                TriggerType.Application,
                null,
                _snapshotCache.CloneCache(),
                _persistingBlock,
                TestProtocolSettings.Default,
                ApplicationEngine.TestModeGas,
                null,
                null) as TracingApplicationEngine;

            Assert.IsNotNull(engine);
            Assert.AreSame(recorder, engine!.TraceRecorder);
            Assert.AreEqual(ExecutionTraceLevel.Syscalls, engine.TraceLevel);
            engine.Dispose();
        }

        [TestMethod]
        public void TracingApplicationEngineProvider_UsesExistingTracingDiagnostic()
        {
            var recorder = new ExecutionTraceRecorder();
            var tracingDiagnostic = new TracingDiagnostic(recorder)
            {
                TraceOpCodes = false,
                TraceContractCalls = false
            };
            var provider = new TracingApplicationEngineProvider(
                traceLevel: ExecutionTraceLevel.OpCodes | ExecutionTraceLevel.ContractCalls);

            using var engine = (TracingApplicationEngine)provider.Create(
                TriggerType.Application,
                null,
                _snapshotCache.CloneCache(),
                _persistingBlock,
                TestProtocolSettings.Default,
                ApplicationEngine.TestModeGas,
                tracingDiagnostic,
                null);

            Assert.AreSame(recorder, engine.TraceRecorder);
            Assert.AreSame(tracingDiagnostic, engine.Diagnostic);
            Assert.IsTrue(tracingDiagnostic.TraceOpCodes);
            Assert.IsTrue(tracingDiagnostic.TraceContractCalls);
        }

        [TestMethod]
        public void TracingApplicationEngineProvider_ComposesDiagnostics()
        {
            var recorder = new ExecutionTraceRecorder();
            var provider = new TracingApplicationEngineProvider(() => recorder, ExecutionTraceLevel.OpCodes);
            var extraDiagnostic = new Mock<IDiagnostic>(MockBehavior.Loose);
            extraDiagnostic.Setup(d => d.Initialized(It.IsAny<ApplicationEngine>()));
            extraDiagnostic.Setup(d => d.ContextLoaded(It.IsAny<ExecutionContext>()));
            extraDiagnostic.Setup(d => d.ContextUnloaded(It.IsAny<ExecutionContext>()));
            extraDiagnostic.Setup(d => d.PreExecuteInstruction(It.IsAny<Instruction>()));
            extraDiagnostic.Setup(d => d.PostExecuteInstruction(It.IsAny<Instruction>()));
            extraDiagnostic.Setup(d => d.Disposed());

            UInt160 scriptHash = UInt160.Parse("0xdddddddddddddddddddddddddddddddddddddddd");
            using (var engine = (TracingApplicationEngine)provider.Create(
                TriggerType.Application,
                null,
                _snapshotCache.CloneCache(),
                _persistingBlock,
                TestProtocolSettings.Default,
                ApplicationEngine.TestModeGas,
                extraDiagnostic.Object,
                null))
            {
                Assert.IsTrue(engine.Diagnostic?.GetType().Name.Contains("DiagnosticCollection") ?? false);

                engine.LoadScript(new byte[] { (byte)OpCode.NOP, (byte)OpCode.RET },
                    configureState: state => state.ScriptHash = scriptHash);
                engine.Execute();
            }

            Assert.IsTrue(recorder.GetOpCodeTraces().Any());
            extraDiagnostic.Verify(d => d.Initialized(It.IsAny<ApplicationEngine>()), Times.Once);
            extraDiagnostic.Verify(d => d.ContextLoaded(It.IsAny<ExecutionContext>()), Times.AtLeastOnce);
            extraDiagnostic.Verify(d => d.ContextUnloaded(It.IsAny<ExecutionContext>()), Times.AtLeastOnce);
            extraDiagnostic.Verify(d => d.PreExecuteInstruction(It.IsAny<Instruction>()), Times.AtLeastOnce);
            extraDiagnostic.Verify(d => d.PostExecuteInstruction(It.IsAny<Instruction>()), Times.AtLeastOnce);
            extraDiagnostic.Verify(d => d.Disposed(), Times.Once);
        }
    }
}

