// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngineProvider.Create.TraceLevel.cs file belongs to the neo project and is free
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

            Assert.AreEqual(ExecutionTraceLevel.All, engine!.TraceLevel);
            engine.Dispose();
        }
    }
}

