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

namespace Neo.UnitTests.Persistence
{
    [TestClass]
    public partial class UT_ExecutionTraceRecorder
    {
        private ExecutionTraceRecorder _recorder = null!;

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
    }
}
