// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngineProvider.Create.Recorder.cs file belongs to the neo project and is free
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

            Assert.AreSame(sharedRecorder, tracingEngine1!.TraceRecorder);
            Assert.AreSame(sharedRecorder, tracingEngine2!.TraceRecorder);
            engine1.Dispose();
            engine2.Dispose();
        }
    }
}

