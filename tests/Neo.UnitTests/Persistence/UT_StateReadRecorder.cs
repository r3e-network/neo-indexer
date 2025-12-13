// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StateReadRecorder.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using System;

namespace Neo.UnitTests.Persistence
{
    [TestClass]
    public sealed class UT_StateReadRecorder
    {
        private static Block CreateBlock(uint index = 1, ulong timestamp = 123)
        {
            return new Block
            {
                Header = new Header
                {
                    Index = index,
                    Timestamp = timestamp,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    NextConsensus = UInt160.Zero
                },
                Transactions = Array.Empty<Transaction>()
            };
        }

        [TestMethod]
        public void IsRecording_ReturnsFalseWhenRecorderIsFull()
        {
            var block = CreateBlock();
            var recorder = new BlockReadRecorder(block, maxEntries: 2);

            using var scope = new BlockReadRecorderScope(recorder, previous: null);

            Assert.IsTrue(StateReadRecorder.IsRecording);

            StateReadRecorder.Record(null, StorageKey.Create(1, 0x01, (byte)0x01), new StorageItem(new byte[] { 0x01 }), "test");
            Assert.IsTrue(StateReadRecorder.IsRecording);

            StateReadRecorder.Record(null, StorageKey.Create(1, 0x01, (byte)0x02), new StorageItem(new byte[] { 0x02 }), "test");
            Assert.IsFalse(StateReadRecorder.IsRecording);

            StateReadRecorder.Record(null, StorageKey.Create(1, 0x01, (byte)0x03), new StorageItem(new byte[] { 0x03 }), "test");
            Assert.AreEqual(2, recorder.Entries.Count);
            Assert.IsTrue(recorder.IsFull);
        }
    }
}

