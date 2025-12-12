// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngineProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.UnitTests.Extensions;
using Neo.VM;
using System;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public class UT_TracingApplicationEngineProvider
    {
        private static DataCache _snapshotCache;
        private static Block _persistingBlock;
        private static ProtocolSettings _settings;

        [TestInitialize]
        public void Setup()
        {
            _snapshotCache = TestBlockchain.GetTestSnapshotCache();
            _settings = TestProtocolSettings.Default;
            _persistingBlock = new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    Timestamp = 0,
                    Index = 0,
                    NextConsensus = UInt160.Zero,
                    Witness = new Witness { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() }
                },
                Transactions = Array.Empty<Transaction>()
            };
        }

        [TestMethod]
        public void Test_DefaultConstructor_CreatesProvider()
        {
            var provider = new TracingApplicationEngineProvider();
            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Test_Constructor_WithRecorderFactory_Succeeds()
        {
            var recorder = new ExecutionTraceRecorder();
            var provider = new TracingApplicationEngineProvider(
                () => recorder,
                ExecutionTraceLevel.All);

            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Test_Constructor_WithFixedRecorder_Succeeds()
        {
            var recorder = new ExecutionTraceRecorder();
            var provider = new TracingApplicationEngineProvider(recorder, ExecutionTraceLevel.All);

            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Test_Constructor_WithNullRecorder_ThrowsException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new TracingApplicationEngineProvider((ExecutionTraceRecorder)null!, ExecutionTraceLevel.All));
        }

        [TestMethod]
        public void Test_Create_ReturnsTracingApplicationEngine()
        {
            var provider = new TracingApplicationEngineProvider();
            var engine = provider.Create(
                TriggerType.Application,
                null,
                _snapshotCache,
                _persistingBlock,
                _settings,
                ApplicationEngine.TestModeGas,
                null,
                null);

            Assert.IsInstanceOfType(engine, typeof(TracingApplicationEngine));
            engine.Dispose();
        }

        [TestMethod]
        public void Test_Create_EngineHasCorrectTraceLevel()
        {
            var provider = new TracingApplicationEngineProvider(
                () => new ExecutionTraceRecorder(),
                ExecutionTraceLevel.Syscalls);

            var engine = provider.Create(
                TriggerType.Application,
                null,
                _snapshotCache,
                _persistingBlock,
                _settings,
                ApplicationEngine.TestModeGas,
                null,
                null);

            var tracingEngine = engine as TracingApplicationEngine;
            Assert.IsNotNull(tracingEngine);
            Assert.AreEqual(ExecutionTraceLevel.Syscalls, tracingEngine.TraceLevel);
            engine.Dispose();
        }

        [TestMethod]
        public void Test_Create_WithTracingDiagnostic_UsesExistingRecorder()
        {
            var recorder = new ExecutionTraceRecorder();
            var diagnostic = new TracingDiagnostic(recorder);
            var provider = new TracingApplicationEngineProvider();

            var engine = provider.Create(
                TriggerType.Application,
                null,
                _snapshotCache,
                _persistingBlock,
                _settings,
                ApplicationEngine.TestModeGas,
                diagnostic,
                null);

            var tracingEngine = engine as TracingApplicationEngine;
            Assert.IsNotNull(tracingEngine);
            Assert.AreSame(recorder, tracingEngine.TraceRecorder);
            engine.Dispose();
        }

        [TestMethod]
        public void Test_Create_WithoutDiagnostic_CreatesNewRecorder()
        {
            var provider = new TracingApplicationEngineProvider(
                () => new ExecutionTraceRecorder(),
                ExecutionTraceLevel.All);

            var engine = provider.Create(
                TriggerType.Application,
                null,
                _snapshotCache,
                _persistingBlock,
                _settings,
                ApplicationEngine.TestModeGas,
                null,
                null);

            var tracingEngine = engine as TracingApplicationEngine;
            Assert.IsNotNull(tracingEngine);
            Assert.IsNotNull(tracingEngine.TraceRecorder);
            engine.Dispose();
        }

        [TestMethod]
        public void Test_Create_MultipleEngines_UseSameFactory()
        {
            int callCount = 0;
            var provider = new TracingApplicationEngineProvider(
                () =>
                {
                    callCount++;
                    return new ExecutionTraceRecorder();
                },
                ExecutionTraceLevel.All);

            var engine1 = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, null, null);

            var engine2 = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, null, null);

            Assert.AreEqual(2, callCount);
            engine1.Dispose();
            engine2.Dispose();
        }

        [TestMethod]
        public void Test_Create_WithFixedRecorder_ReusesRecorder()
        {
            var sharedRecorder = new ExecutionTraceRecorder();
            var provider = new TracingApplicationEngineProvider(sharedRecorder, ExecutionTraceLevel.All);

            var engine1 = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, null, null);

            var engine2 = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, null, null);

            var tracingEngine1 = engine1 as TracingApplicationEngine;
            var tracingEngine2 = engine2 as TracingApplicationEngine;

            Assert.AreSame(sharedRecorder, tracingEngine1.TraceRecorder);
            Assert.AreSame(sharedRecorder, tracingEngine2.TraceRecorder);
            engine1.Dispose();
            engine2.Dispose();
        }

        [TestMethod]
        public void Test_Provider_ImplementsIApplicationEngineProvider()
        {
            var provider = new TracingApplicationEngineProvider();
            Assert.IsInstanceOfType(provider, typeof(IApplicationEngineProvider));
        }

        [TestMethod]
        public void Test_Create_WithDifferentTriggerTypes()
        {
            var provider = new TracingApplicationEngineProvider();

            var appEngine = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, null, null);
            Assert.AreEqual(TriggerType.Application, appEngine.Trigger);

            var verifyEngine = provider.Create(
                TriggerType.Verification, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, null, null);
            Assert.AreEqual(TriggerType.Verification, verifyEngine.Trigger);

            appEngine.Dispose();
            verifyEngine.Dispose();
        }

        [TestMethod]
        public void Test_Create_PassesCorrectGasLimit()
        {
            var provider = new TracingApplicationEngineProvider();
            long gasLimit = 50_000_000;

            var engine = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, gasLimit, null, null);

            Assert.AreEqual(gasLimit, engine.GasLeft + engine.FeeConsumed);
            engine.Dispose();
        }

        [TestMethod]
        public void Test_Create_TraceLevel_All_ConfiguresDiagnostic()
        {
            var recorder = new ExecutionTraceRecorder();
            var diagnostic = new TracingDiagnostic(recorder);
            var provider = new TracingApplicationEngineProvider(
                recorder,
                ExecutionTraceLevel.All);

            var engine = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, diagnostic, null);

            Assert.IsTrue(diagnostic.TraceOpCodes);
            Assert.IsTrue(diagnostic.TraceContractCalls);
            engine.Dispose();
        }

        [TestMethod]
        public void Test_Create_TraceLevel_None_DisablesDiagnostic()
        {
            var recorder = new ExecutionTraceRecorder();
            var diagnostic = new TracingDiagnostic(recorder);
            var provider = new TracingApplicationEngineProvider(
                recorder,
                ExecutionTraceLevel.None);

            var engine = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, diagnostic, null);

            Assert.IsFalse(diagnostic.TraceOpCodes);
            Assert.IsFalse(diagnostic.TraceContractCalls);
            engine.Dispose();
        }

        [TestMethod]
        public void Test_Create_TraceLevel_OpCodesOnly()
        {
            var recorder = new ExecutionTraceRecorder();
            var diagnostic = new TracingDiagnostic(recorder);
            var provider = new TracingApplicationEngineProvider(
                recorder,
                ExecutionTraceLevel.OpCodes);

            var engine = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, diagnostic, null);

            Assert.IsTrue(diagnostic.TraceOpCodes);
            Assert.IsFalse(diagnostic.TraceContractCalls);
            engine.Dispose();
        }

        [TestMethod]
        public void Test_Create_TraceLevel_ContractCallsOnly()
        {
            var recorder = new ExecutionTraceRecorder();
            var diagnostic = new TracingDiagnostic(recorder);
            var provider = new TracingApplicationEngineProvider(
                recorder,
                ExecutionTraceLevel.ContractCalls);

            var engine = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, diagnostic, null);

            Assert.IsFalse(diagnostic.TraceOpCodes);
            Assert.IsTrue(diagnostic.TraceContractCalls);
            engine.Dispose();
        }

        [TestMethod]
        public void Test_DefaultTraceLevel_IsAll()
        {
            var recorder = new ExecutionTraceRecorder();
            var provider = new TracingApplicationEngineProvider(recorder);

            var engine = provider.Create(
                TriggerType.Application, null, _snapshotCache, _persistingBlock,
                _settings, ApplicationEngine.TestModeGas, null, null) as TracingApplicationEngine;

            Assert.AreEqual(ExecutionTraceLevel.All, engine.TraceLevel);
            engine.Dispose();
        }
    }
}
