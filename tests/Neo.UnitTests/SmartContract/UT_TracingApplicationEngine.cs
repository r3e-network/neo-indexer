// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngine.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Linq;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public class UT_TracingApplicationEngine
    {
        private static readonly byte[] DefaultScript = [(byte)OpCode.RET];
        private DataCache _snapshotCache = null!;
        private Block _persistingBlock = null!;

        [TestInitialize]
        public void Setup()
        {
            _snapshotCache = TestBlockchain.GetTestSnapshotCache();
            _persistingBlock = new Block
            {
                Header = new Header
                {
                    Index = 0,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    Timestamp = 0,
                    Nonce = 0,
                    NextConsensus = UInt160.Zero,
                    Witness = new Witness
                    {
                        InvocationScript = Array.Empty<byte>(),
                        VerificationScript = Array.Empty<byte>()
                    }
                },
                Transactions = Array.Empty<Transaction>()
            };
        }

        [DataTestMethod]
        [DataRow(ExecutionTraceLevel.None, false, false, false, false, false)]
        [DataRow(ExecutionTraceLevel.Syscalls, true, false, false, false, false)]
        [DataRow(ExecutionTraceLevel.Storage, false, true, false, false, false)]
        [DataRow(ExecutionTraceLevel.Notifications, false, false, true, false, false)]
        [DataRow(ExecutionTraceLevel.ContractCalls, false, false, false, true, false)]
        [DataRow(ExecutionTraceLevel.OpCodes, false, false, false, false, true)]
        [DataRow(ExecutionTraceLevel.All, true, true, true, true, true)]
        public void ExecutionTraceLevel_CombinationsBehaveAsFlags(
            ExecutionTraceLevel level,
            bool syscalls,
            bool storage,
            bool notifications,
            bool contractCalls,
            bool opcodes)
        {
            Assert.AreEqual(syscalls, level.HasFlag(ExecutionTraceLevel.Syscalls));
            Assert.AreEqual(storage, level.HasFlag(ExecutionTraceLevel.Storage));
            Assert.AreEqual(notifications, level.HasFlag(ExecutionTraceLevel.Notifications));
            Assert.AreEqual(contractCalls, level.HasFlag(ExecutionTraceLevel.ContractCalls));
            Assert.AreEqual(opcodes, level.HasFlag(ExecutionTraceLevel.OpCodes));
        }

        [TestMethod]
        public void ExecutionTraceLevel_AllIncludesEveryFlag()
        {
            var expected = ExecutionTraceLevel.Syscalls |
                           ExecutionTraceLevel.Storage |
                           ExecutionTraceLevel.Notifications |
                           ExecutionTraceLevel.ContractCalls |
                           ExecutionTraceLevel.OpCodes;
            Assert.AreEqual(expected, ExecutionTraceLevel.All);
        }

        [TestMethod]
        public void TracingApplicationEngine_Constructor_SetsProperties()
        {
            var recorder = new ExecutionTraceRecorder();
            var traceLevel = ExecutionTraceLevel.Syscalls | ExecutionTraceLevel.Storage;

            using var engine = new TracingApplicationEngine(
                TriggerType.Application,
                null,
                _snapshotCache.CloneCache(),
                _persistingBlock,
                TestProtocolSettings.Default,
                ApplicationEngine.TestModeGas,
                recorder,
                traceLevel);

            Assert.AreSame(recorder, engine.TraceRecorder);
            Assert.AreEqual(traceLevel, engine.TraceLevel);
        }

        [TestMethod]
        public void TracingApplicationEngine_Constructor_ThrowsOnNullRecorder()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new TracingApplicationEngine(
                    TriggerType.Application,
                    null,
                    _snapshotCache.CloneCache(),
                    _persistingBlock,
                    TestProtocolSettings.Default,
                    ApplicationEngine.TestModeGas,
                    null!,
                    ExecutionTraceLevel.All));
        }

        [TestMethod]
        public void TracingApplicationEngine_ExecutesScript()
        {
            var recorder = new ExecutionTraceRecorder();
            using var engine = new TracingApplicationEngine(
                TriggerType.Application,
                null,
                _snapshotCache.CloneCache(),
                _persistingBlock,
                TestProtocolSettings.Default,
                ApplicationEngine.TestModeGas,
                recorder,
                ExecutionTraceLevel.All);

            engine.LoadScript(new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET });
            var result = engine.Execute();

            Assert.AreEqual(VMState.HALT, result);
        }

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

        [TestMethod]
        public void TracingApplicationEngine_TraceLevel_None_NoTracing()
        {
            var recorder = new ExecutionTraceRecorder();
            using var engine = new TracingApplicationEngine(
                TriggerType.Application,
                null,
                _snapshotCache.CloneCache(),
                _persistingBlock,
                TestProtocolSettings.Default,
                ApplicationEngine.TestModeGas,
                recorder,
                ExecutionTraceLevel.None);

            engine.LoadScript(new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET });
            engine.Execute();

            Assert.AreEqual(0, recorder.GetOpCodeTraces().Count);
            Assert.AreEqual(0, recorder.GetSyscallTraces().Count);
        }

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
