// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ExecutionTraceRecorder.cs file belongs to the neo project and is free
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
using System.Threading.Tasks;

namespace Neo.UnitTests.Persistence
{
    [TestClass]
    public class UT_ExecutionTraceRecorder
    {
        private ExecutionTraceRecorder _recorder;

        [TestInitialize]
        public void Setup()
        {
            _recorder = new ExecutionTraceRecorder
            {
                BlockIndex = 100,
                TxHash = UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000001"),
                IsEnabled = true
            };
        }

        [TestMethod]
        public void Test_DefaultConstructor()
        {
            var recorder = new ExecutionTraceRecorder();
            Assert.IsTrue(recorder.IsEnabled);
            Assert.AreEqual(0u, recorder.BlockIndex);
            Assert.IsNull(recorder.TxHash);
        }

        [TestMethod]
        public void Test_BlockIndex_SetGet()
        {
            _recorder.BlockIndex = 12345;
            Assert.AreEqual(12345u, _recorder.BlockIndex);
        }

        [TestMethod]
        public void Test_TxHash_SetGet()
        {
            var hash = UInt256.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
            _recorder.TxHash = hash;
            Assert.AreEqual(hash, _recorder.TxHash);
        }

        [TestMethod]
        public void Test_IsEnabled_SetGet()
        {
            _recorder.IsEnabled = false;
            Assert.IsFalse(_recorder.IsEnabled);
            _recorder.IsEnabled = true;
            Assert.IsTrue(_recorder.IsEnabled);
        }

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
        public void Test_RecordContractCall_WithParams()
        {
            var caller = UInt160.Parse("0x0000000000000000000000000000000000000001");
            var callee = UInt160.Parse("0x0000000000000000000000000000000000000002");
            var trace = _recorder.RecordContractCall(caller, callee, "transfer", 2);

            Assert.AreEqual(caller, trace.CallerHash);
            Assert.AreEqual(callee, trace.CalleeHash);
            Assert.AreEqual("transfer", trace.MethodName);
            Assert.AreEqual(2, trace.CallDepth);
            Assert.AreEqual(0, trace.Order);
        }

        [TestMethod]
        public void Test_RecordContractCall_NullCaller()
        {
            var callee = UInt160.Zero;
            var trace = _recorder.RecordContractCall(null, callee, "main", 1);

            Assert.IsNull(trace.CallerHash);
            Assert.AreEqual(callee, trace.CalleeHash);
        }

        [TestMethod]
        public void Test_RecordStorageWrite_WithParams()
        {
            var contractHash = UInt160.Parse("0x0000000000000000000000000000000000000003");
            byte[] key = { 0x01, 0x02, 0x03 };
            byte[] oldValue = { 0x10, 0x20 };
            byte[] newValue = { 0x30, 0x40, 0x50 };

            var trace = _recorder.RecordStorageWrite(
                1,
                contractHash,
                key,
                oldValue,
                newValue);

            Assert.AreEqual(1, trace.ContractId);
            Assert.AreEqual(contractHash, trace.ContractHash);
            CollectionAssert.AreEqual(key, trace.Key.ToArray());
            CollectionAssert.AreEqual(oldValue, trace.OldValue?.ToArray());
            CollectionAssert.AreEqual(newValue, trace.NewValue.ToArray());
            Assert.AreEqual(0, trace.Order);
        }

        [TestMethod]
        public void Test_RecordStorageWrite_NullOldValue()
        {
            var trace = _recorder.RecordStorageWrite(
                1,
                UInt160.Zero,
                new byte[] { 0x01 },
                null,
                new byte[] { 0x02 });

            Assert.IsNull(trace.OldValue);
        }

        [TestMethod]
        public void Test_RecordNotification_WithParams()
        {
            var contractHash = UInt160.Parse("0x0000000000000000000000000000000000000004");
            var trace = _recorder.RecordNotification(contractHash, "Transfer", "[\"from\",\"to\",100]");

            Assert.AreEqual(contractHash, trace.ContractHash);
            Assert.AreEqual("Transfer", trace.EventName);
            Assert.AreEqual("[\"from\",\"to\",100]", trace.StateJson);
            Assert.AreEqual(0, trace.Order);
        }

        [TestMethod]
        public void Test_RecordNotification_NullStateJson()
        {
            var trace = _recorder.RecordNotification(UInt160.Zero, "Event", null);
            Assert.IsNull(trace.StateJson);
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
        public void Test_GetContractCallTraces_OrderedByOrder()
        {
            _recorder.RecordContractCall(null, UInt160.Zero, "first", 1);
            _recorder.RecordContractCall(null, UInt160.Zero, "second", 2);

            var traces = _recorder.GetContractCallTraces();

            Assert.AreEqual(2, traces.Count);
            Assert.AreEqual("first", traces[0].MethodName);
            Assert.AreEqual("second", traces[1].MethodName);
        }

        [TestMethod]
        public void Test_GetStorageWriteTraces_OrderedByOrder()
        {
            _recorder.RecordStorageWrite(1, UInt160.Zero, new byte[] { 1 }, null, new byte[] { 1 });
            _recorder.RecordStorageWrite(2, UInt160.Zero, new byte[] { 2 }, null, new byte[] { 2 });

            var traces = _recorder.GetStorageWriteTraces();

            Assert.AreEqual(2, traces.Count);
            Assert.AreEqual(1, traces[0].ContractId);
            Assert.AreEqual(2, traces[1].ContractId);
        }

        [TestMethod]
        public void Test_GetNotificationTraces_OrderedByOrder()
        {
            _recorder.RecordNotification(UInt160.Zero, "First", null);
            _recorder.RecordNotification(UInt160.Zero, "Second", null);

            var traces = _recorder.GetNotificationTraces();

            Assert.AreEqual(2, traces.Count);
            Assert.AreEqual("First", traces[0].EventName);
            Assert.AreEqual("Second", traces[1].EventName);
        }

        [TestMethod]
        public void Test_GetStats_ReturnsCorrectCounts()
        {
            _recorder.RecordOpCode(UInt160.Zero, 0, OpCode.NOP, default, 100, 0);
            _recorder.RecordOpCode(UInt160.Zero, 1, OpCode.NOP, default, 200, 0);
            _recorder.RecordSyscall(UInt160.Zero, 0, "syscall", 50);
            _recorder.RecordContractCall(null, UInt160.Zero, "method", 1);
            _recorder.RecordStorageWrite(1, UInt160.Zero, new byte[] { 1 }, null, new byte[] { 1 });
            _recorder.RecordNotification(UInt160.Zero, "event", null);

            var stats = _recorder.GetStats();

            Assert.AreEqual(100u, stats.BlockIndex);
            Assert.AreEqual(1, stats.TransactionCount);
            Assert.AreEqual(2, stats.OpCodeCount);
            Assert.AreEqual(1, stats.SyscallCount);
            Assert.AreEqual(1, stats.ContractCallCount);
            Assert.AreEqual(1, stats.StorageWriteCount);
            Assert.AreEqual(1, stats.NotificationCount);
        }

        [TestMethod]
        public void Test_Clear_RemovesAllTraces()
        {
            _recorder.RecordOpCode(UInt160.Zero, 0, OpCode.NOP, default, 0, 0);
            _recorder.RecordSyscall(UInt160.Zero, 0, "syscall", 0);
            _recorder.RecordContractCall(null, UInt160.Zero, "method", 1);
            _recorder.RecordStorageWrite(1, UInt160.Zero, new byte[] { 1 }, null, new byte[] { 1 });
            _recorder.RecordNotification(UInt160.Zero, "event", null);

            Assert.IsTrue(_recorder.HasTraces);

            _recorder.Clear();

            Assert.IsFalse(_recorder.HasTraces);
            Assert.AreEqual(0, _recorder.GetOpCodeTraces().Count);
            Assert.AreEqual(0, _recorder.GetSyscallTraces().Count);
            Assert.AreEqual(0, _recorder.GetContractCallTraces().Count);
            Assert.AreEqual(0, _recorder.GetStorageWriteTraces().Count);
            Assert.AreEqual(0, _recorder.GetNotificationTraces().Count);
        }

        [TestMethod]
        public void Test_Clear_ResetsOrderCounters()
        {
            _recorder.RecordOpCode(UInt160.Zero, 0, OpCode.NOP, default, 0, 0);
            _recorder.RecordOpCode(UInt160.Zero, 1, OpCode.NOP, default, 0, 0);

            _recorder.Clear();

            var trace = _recorder.RecordOpCode(UInt160.Zero, 0, OpCode.NOP, default, 0, 0);
            Assert.AreEqual(0, trace.Order);
        }

        [TestMethod]
        public void Test_HasTraces_EmptyRecorder_ReturnsFalse()
        {
            var recorder = new ExecutionTraceRecorder();
            Assert.IsFalse(recorder.HasTraces);
        }

        [TestMethod]
        public void Test_HasTraces_WithOpCode_ReturnsTrue()
        {
            _recorder.Clear();
            _recorder.RecordOpCode(UInt160.Zero, 0, OpCode.NOP, default, 0, 0);
            Assert.IsTrue(_recorder.HasTraces);
        }

        [TestMethod]
        public void Test_HasTraces_WithSyscall_ReturnsTrue()
        {
            _recorder.Clear();
            _recorder.RecordSyscall(UInt160.Zero, 0, "syscall", 0);
            Assert.IsTrue(_recorder.HasTraces);
        }

        [TestMethod]
        public void Test_ConcurrentRecording_ThreadSafe()
        {
            var recorder = new ExecutionTraceRecorder { IsEnabled = true };
            var tasks = new Task[10];

            for (int i = 0; i < tasks.Length; i++)
            {
                int index = i;
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
