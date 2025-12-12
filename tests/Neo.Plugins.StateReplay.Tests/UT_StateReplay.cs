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
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.UnitTests;
using System;
using System.IO;
using System.Text;
using StateReplay;
using Neo;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace StateReplay.Tests
{
    [TestClass]
    public class UT_StateReplay
    {
        private StateReplayPlugin _plugin = null!;
        private Neo.NeoSystem _system = null!;

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

        #region Binary Format Tests

        private static byte[] CreateValidBinaryFile(uint blockIndex, int entryCount = 0)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            // Magic "NSBR"
            writer.Write((byte)'N');
            writer.Write((byte)'S');
            writer.Write((byte)'B');
            writer.Write((byte)'R');

            // Version
            writer.Write((ushort)1);

            // Block index
            writer.Write(blockIndex);

            // Entry count
            writer.Write(entryCount);

            // Create storage key format entries
            for (int i = 0; i < entryCount; i++)
            {
                // ContractHash (20 bytes)
                writer.Write(new byte[20]);

                // Key: Use a format compatible with StorageKey
                // StorageKey = [ContractId (4 bytes)][Key bytes]
                var keyBytes = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01, 0x02 };
                writer.Write((ushort)keyBytes.Length);
                writer.Write(keyBytes);

                // Value
                var valueBytes = new byte[] { 0x03, 0x04, 0x05 };
                writer.Write(valueBytes.Length);
                writer.Write(valueBytes);

                // ReadOrder
                writer.Write(i);
            }

            return ms.ToArray();
        }

        [TestMethod]
        public void ReplayBinary_AcceptsValidFile()
        {
            var bytes = CreateValidBinaryFile(0u, 1);
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, bytes);

            // Should not throw - block 0 exists in test blockchain
            _plugin.ReplayBinaryForTest(path);
        }

        [TestMethod]
        public void ReplayBinary_RejectsNonExistentBlock()
        {
            var bytes = CreateValidBinaryFile(999999u, 0);
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, bytes);

            Assert.ThrowsExactly<InvalidOperationException>(() => _plugin.ReplayBinaryForTest(path));
        }

        [TestMethod]
        public void ReplayBinary_RejectsMissingFile()
        {
            Assert.ThrowsExactly<FileNotFoundException>(() => _plugin.ReplayBinaryForTest("/nonexistent/path/file.bin"));
        }

        [TestMethod]
        public void Replay_AutoDetectsBinaryFormat()
        {
            var bytes = CreateValidBinaryFile(0u, 1);
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, bytes);

            // ReplayForTest should auto-detect binary format and call ReplayBinaryForTest
            _plugin.ReplayForTest(path);
        }

        #endregion

        #region Plugin Property Tests

        [TestMethod]
        public void Plugin_HasCorrectDescription()
        {
            Assert.IsTrue(_plugin.Description.Contains("Replay"));
        }

        [TestMethod]
        public void Plugin_ConfigFileHasCorrectPath()
        {
            Assert.IsTrue(_plugin.ConfigFile.EndsWith("StateReplay.json"));
        }

        #endregion
    }
}
