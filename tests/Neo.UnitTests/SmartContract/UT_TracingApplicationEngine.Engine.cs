// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngine.Engine.cs file belongs to the neo project and is free
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
using System;

namespace Neo.UnitTests.SmartContract
{
    public partial class UT_TracingApplicationEngine
    {
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
    }
}

