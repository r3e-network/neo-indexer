// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngineProvider.cs file belongs to the neo project and is free
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
using Neo.UnitTests.Extensions;
using Neo.VM;
using System;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public partial class UT_TracingApplicationEngineProvider
    {
        private static DataCache _snapshotCache = null!;
        private static Block _persistingBlock = null!;
        private static ProtocolSettings _settings = null!;

        [TestInitialize]
        public void Setup()
        {
            _snapshotCache = TestBlockchain.GetTestSnapshotCache();
            _settings = TestProtocolSettings.Default;
            _persistingBlock = new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    Timestamp = 0,
                    Index = 0,
                    NextConsensus = UInt160.Zero,
                    Witness = new Witness { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() }
                },
                Transactions = Array.Empty<Transaction>()
            };
        }

        [TestMethod]
        public void Test_DefaultConstructor_CreatesProvider()
        {
            var provider = new TracingApplicationEngineProvider();
            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Test_Constructor_WithRecorderFactory_Succeeds()
        {
            var recorder = new ExecutionTraceRecorder();
            var provider = new TracingApplicationEngineProvider(
                () => recorder,
                ExecutionTraceLevel.All);

            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Test_Constructor_WithFixedRecorder_Succeeds()
        {
            var recorder = new ExecutionTraceRecorder();
            var provider = new TracingApplicationEngineProvider(recorder, ExecutionTraceLevel.All);

            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Test_Constructor_WithNullRecorder_ThrowsException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new TracingApplicationEngineProvider((ExecutionTraceRecorder)null!, ExecutionTraceLevel.All));
        }

        [TestMethod]
        public void Test_Provider_ImplementsIApplicationEngineProvider()
        {
            var provider = new TracingApplicationEngineProvider();
            Assert.IsInstanceOfType(provider, typeof(IApplicationEngineProvider));
        }
    }
}

