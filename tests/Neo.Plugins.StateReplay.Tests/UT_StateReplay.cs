// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StateReplay.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.UnitTests;
using System;
using System.IO;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace StateReplay.Tests
{
    [TestClass]
    public partial class UT_StateReplay
    {
        private StateReplayPlugin _plugin = null!;
        private NeoSystem _system = null!;

        [TestInitialize]
        public void Setup()
        {
            _system = TestBlockchain.GetSystem();
            _plugin = new StateReplayPlugin();
            _plugin.LoadForTest(_system);
        }

        [TestMethod]
        public void ReplayRejectsHeightMismatch()
        {
            var path = Path.GetTempFileName();
            var doc = new { block = 1u, keyCount = 0, keys = Array.Empty<object>() };
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(doc));
            Assert.ThrowsExactly<InvalidOperationException>(() => _plugin.ReplayForTest(path, 2));
        }

        [TestMethod]
        public void ReplayAcceptsMatchingSnapshot()
        {
            var path = Path.GetTempFileName();
            var key = StorageKey.Create(0, 0x01);
            var doc = new
            {
                block = 0u,
                hash = NativeContract.Ledger.GetBlockHash(_system.StoreView, 0)?.ToString(),
                keyCount = 1,
                keys = new[]
                {
                    new {
                        key = Convert.ToBase64String(key.ToArray()),
                        value = Convert.ToBase64String(new byte[]{0x01}),
                        readOrder = 1
                    }
                }
            };
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(doc));
            _plugin.ReplayForTest(path, 0);
        }

        [TestMethod]
        public void ReplayRejectsMissingKeys()
        {
            var path = Path.GetTempFileName();
            var doc = new { block = 0u };
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(doc));
            Assert.ThrowsExactly<InvalidOperationException>(() => _plugin.ReplayForTest(path, 0));
        }

        [TestMethod]
        public void ReplayRejectsMissingBlock()
        {
            var path = Path.GetTempFileName();
            var doc = new { keyCount = 0, keys = Array.Empty<object>() };
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(doc));
            Assert.ThrowsExactly<InvalidOperationException>(() => _plugin.ReplayForTest(path, null));
        }

        [TestMethod]
        public void ReplayRejectsKeyCountMismatch()
        {
            var path = Path.GetTempFileName();
            var key = StorageKey.Create(0, 0x02);
            var doc = new
            {
                block = 0u,
                hash = NativeContract.Ledger.GetBlockHash(_system.StoreView, 0)?.ToString(),
                keyCount = 2,
                keys = new[]
                {
                    new {
                        key = Convert.ToBase64String(key.ToArray()),
                        value = Convert.ToBase64String(new byte[]{0x01}),
                        readOrder = 1
                    }
                }
            };
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(doc));
            Assert.ThrowsExactly<InvalidOperationException>(() => _plugin.ReplayForTest(path, 0));
        }

        [TestMethod]
        public void ReplayRejectsInvalidBase64()
        {
            var path = Path.GetTempFileName();
            var doc = new
            {
                block = 0u,
                hash = NativeContract.Ledger.GetBlockHash(_system.StoreView, 0)?.ToString(),
                keyCount = 1,
                keys = new[]
                {
                    new {
                        key = "!!",
                        value = Convert.ToBase64String(new byte[]{0x01}),
                        readOrder = 1
                    }
                }
            };
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(doc));
            Assert.ThrowsExactly<FormatException>(() => _plugin.ReplayForTest(path, 0));
        }

        [TestMethod]
        public void ReplayRejectsMissingHashBlock()
        {
            var path = Path.GetTempFileName();
            var doc = new
            {
                block = 0u,
                keyCount = 0,
                keys = Array.Empty<object>()
            };
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(doc));
            Assert.ThrowsExactly<InvalidOperationException>(() => _plugin.ReplayForTest(path, 0));
        }

        [TestMethod]
        public void ReplayRejectsInvalidJson()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "{ not json");
            Assert.ThrowsExactly<InvalidOperationException>(() => _plugin.ReplayForTest(path, null));
        }

        [TestMethod]
        public void ReplayRejectsHeightHashMismatch()
        {
            var path = Path.GetTempFileName();
            var wrongHash = UInt256.Zero.ToString();
            var doc = new
            {
                block = 1u,
                hash = wrongHash,
                keyCount = 0,
                keys = Array.Empty<object>()
            };
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(doc));
            Assert.ThrowsExactly<InvalidOperationException>(() => _plugin.ReplayForTest(path, 1));
        }
    }
}

