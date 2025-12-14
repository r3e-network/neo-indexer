// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngineProvider.Create.cs file belongs to the neo project and is free
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

namespace Neo.UnitTests.SmartContract
{
    public partial class UT_TracingApplicationEngineProvider
    {
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

