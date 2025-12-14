// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ExecutionTraceRecorder.Concurrency.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using Neo.VM;
using System.Threading.Tasks;

namespace Neo.UnitTests.Persistence
{
    public partial class UT_ExecutionTraceRecorder
    {
        [TestMethod]
        public void Test_ConcurrentRecording_ThreadSafe()
        {
            var recorder = new ExecutionTraceRecorder { IsEnabled = true };
            var tasks = new Task[10];

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        recorder.RecordOpCode(UInt160.Zero, j, OpCode.NOP, default, 0, 0);
                    }
                });
            }

            Task.WaitAll(tasks);

            var traces = recorder.GetOpCodeTraces();
            Assert.AreEqual(1000, traces.Count);
        }
    }
}

