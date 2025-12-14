// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngine.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using System;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public partial class UT_TracingApplicationEngine
    {
        private DataCache _snapshotCache = null!;
        private Block _persistingBlock = null!;

        [TestInitialize]
        public void Setup()
        {
            _snapshotCache = TestBlockchain.GetTestSnapshotCache();
            _persistingBlock = new Block
            {
                Header = new Header
                {
                    Index = 0,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    Timestamp = 0,
                    Nonce = 0,
                    NextConsensus = UInt160.Zero,
                    Witness = new Witness
                    {
                        InvocationScript = Array.Empty<byte>(),
                        VerificationScript = Array.Empty<byte>()
                    }
                },
                Transactions = Array.Empty<Transaction>()
            };
        }

        [DataTestMethod]
        [DataRow(ExecutionTraceLevel.None, false, false, false, false, false)]
        [DataRow(ExecutionTraceLevel.Syscalls, true, false, false, false, false)]
        [DataRow(ExecutionTraceLevel.Storage, false, true, false, false, false)]
        [DataRow(ExecutionTraceLevel.Notifications, false, false, true, false, false)]
        [DataRow(ExecutionTraceLevel.ContractCalls, false, false, false, true, false)]
        [DataRow(ExecutionTraceLevel.OpCodes, false, false, false, false, true)]
        [DataRow(ExecutionTraceLevel.All, true, true, true, true, true)]
        public void ExecutionTraceLevel_CombinationsBehaveAsFlags(
            ExecutionTraceLevel level,
            bool syscalls,
            bool storage,
            bool notifications,
            bool contractCalls,
            bool opcodes)
        {
            Assert.AreEqual(syscalls, level.HasFlag(ExecutionTraceLevel.Syscalls));
            Assert.AreEqual(storage, level.HasFlag(ExecutionTraceLevel.Storage));
            Assert.AreEqual(notifications, level.HasFlag(ExecutionTraceLevel.Notifications));
            Assert.AreEqual(contractCalls, level.HasFlag(ExecutionTraceLevel.ContractCalls));
            Assert.AreEqual(opcodes, level.HasFlag(ExecutionTraceLevel.OpCodes));
        }

        [TestMethod]
        public void ExecutionTraceLevel_AllIncludesEveryFlag()
        {
            var expected = ExecutionTraceLevel.Syscalls |
                           ExecutionTraceLevel.Storage |
                           ExecutionTraceLevel.Notifications |
                           ExecutionTraceLevel.ContractCalls |
                           ExecutionTraceLevel.OpCodes;
            Assert.AreEqual(expected, ExecutionTraceLevel.All);
        }
    }
}

