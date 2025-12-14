// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngineProvider.Create.Basic.cs file belongs to the neo project and is free
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
    }
}

