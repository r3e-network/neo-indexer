// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ExecutionTraceRecorder.CallsWritesNotifications.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using System;

namespace Neo.UnitTests.Persistence
{
    public partial class UT_ExecutionTraceRecorder
    {
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
        public void Test_GetNotificationTraces_OrderedByOrder()
        {
            _recorder.RecordNotification(UInt160.Zero, "First", null);
            _recorder.RecordNotification(UInt160.Zero, "Second", null);

            var traces = _recorder.GetNotificationTraces();

            Assert.AreEqual(2, traces.Count);
            Assert.AreEqual("First", traces[0].EventName);
            Assert.AreEqual("Second", traces[1].EventName);
        }
    }
}

