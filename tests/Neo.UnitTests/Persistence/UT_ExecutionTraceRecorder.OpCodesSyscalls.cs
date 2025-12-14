// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ExecutionTraceRecorder.OpCodesSyscalls.cs file belongs to the neo project and is free
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
using System;

namespace Neo.UnitTests.Persistence
{
    public partial class UT_ExecutionTraceRecorder
    {
        [TestMethod]
        public void Test_RecordOpCode_WithParams()
        {
            var contractHash = UInt160.Parse("0x0000000000000000000000000000000000000001");
            var trace = _recorder.RecordOpCode(
                contractHash,
                10,
                OpCode.PUSH1,
                ReadOnlyMemory<byte>.Empty,
                1000,
                5);

            Assert.AreEqual(contractHash, trace.ContractHash);
            Assert.AreEqual(10, trace.InstructionPointer);
            Assert.AreEqual(OpCode.PUSH1, trace.OpCode);
            Assert.AreEqual(1000, trace.GasConsumed);
            Assert.AreEqual(5, trace.StackDepth);
            Assert.AreEqual(0, trace.Order);
        }

        [TestMethod]
        public void Test_RecordOpCode_AutoIncrementsOrder()
        {
            var contractHash = UInt160.Zero;
            var trace1 = _recorder.RecordOpCode(contractHash, 0, OpCode.NOP, default, 0, 0);
            var trace2 = _recorder.RecordOpCode(contractHash, 1, OpCode.NOP, default, 0, 0);
            var trace3 = _recorder.RecordOpCode(contractHash, 2, OpCode.NOP, default, 0, 0);

            Assert.AreEqual(0, trace1.Order);
            Assert.AreEqual(1, trace2.Order);
            Assert.AreEqual(2, trace3.Order);
        }

        [TestMethod]
        public void Test_RecordOpCode_Disabled_StillReturnsTrace()
        {
            _recorder.IsEnabled = false;
            var trace = _recorder.RecordOpCode(UInt160.Zero, 0, OpCode.NOP, default, 0, 0);

            Assert.IsNotNull(trace);
            var traces = _recorder.GetOpCodeTraces();
            Assert.AreEqual(0, traces.Count);
        }

        [TestMethod]
        public void Test_GetOpCodeTraces_OrderedByOrder()
        {
            _recorder.RecordOpCode(UInt160.Zero, 0, OpCode.PUSH1, default, 0, 0);
            _recorder.RecordOpCode(UInt160.Zero, 1, OpCode.PUSH2, default, 0, 0);
            _recorder.RecordOpCode(UInt160.Zero, 2, OpCode.ADD, default, 0, 0);

            var traces = _recorder.GetOpCodeTraces();

            Assert.AreEqual(3, traces.Count);
            Assert.AreEqual(0, traces[0].Order);
            Assert.AreEqual(1, traces[1].Order);
            Assert.AreEqual(2, traces[2].Order);
        }

        [TestMethod]
        public void Test_RecordOpCode_PreAllocated_Trace()
        {
            var trace = new OpCodeTrace
            {
                ContractHash = UInt160.Zero,
                InstructionPointer = 5,
                OpCode = OpCode.SYSCALL,
                GasConsumed = 999,
                StackDepth = 10,
                Order = 42
            };

            _recorder.RecordOpCode(trace);
            var traces = _recorder.GetOpCodeTraces();

            Assert.AreEqual(1, traces.Count);
            Assert.AreEqual(42, traces[0].Order);
        }

        [TestMethod]
        public void Test_RecordSyscall_WithParams()
        {
            var contractHash = UInt160.Parse("0x0000000000000000000000000000000000000002");
            var trace = _recorder.RecordSyscall(
                contractHash,
                0x12345678,
                "System.Storage.Get",
                50000);

            Assert.AreEqual(contractHash, trace.ContractHash);
            Assert.AreEqual("12345678", trace.SyscallHash);
            Assert.AreEqual("System.Storage.Get", trace.SyscallName);
            Assert.AreEqual(50000, trace.GasCost);
            Assert.AreEqual(0, trace.Order);
        }

        [TestMethod]
        public void Test_RecordSyscall_AutoIncrementsOrder()
        {
            var trace1 = _recorder.RecordSyscall(UInt160.Zero, 0, "syscall1", 0);
            var trace2 = _recorder.RecordSyscall(UInt160.Zero, 0, "syscall2", 0);

            Assert.AreEqual(0, trace1.Order);
            Assert.AreEqual(1, trace2.Order);
        }

        [TestMethod]
        public void Test_GetSyscallTraces_OrderedByOrder()
        {
            _recorder.RecordSyscall(UInt160.Zero, 1, "first", 0);
            _recorder.RecordSyscall(UInt160.Zero, 2, "second", 0);

            var traces = _recorder.GetSyscallTraces();

            Assert.AreEqual(2, traces.Count);
            Assert.AreEqual("first", traces[0].SyscallName);
            Assert.AreEqual("second", traces[1].SyscallName);
        }

        [TestMethod]
        public void Test_RecordSyscall_PreAllocated_Trace()
        {
            var trace = new SyscallTrace
            {
                ContractHash = UInt160.Zero,
                SyscallHash = "AABBCCDD",
                SyscallName = "Test.Syscall",
                GasCost = 100,
                Order = 99
            };

            _recorder.RecordSyscall(trace);
            var traces = _recorder.GetSyscallTraces();

            Assert.AreEqual(1, traces.Count);
            Assert.AreEqual(99, traces[0].Order);
        }
    }
}

