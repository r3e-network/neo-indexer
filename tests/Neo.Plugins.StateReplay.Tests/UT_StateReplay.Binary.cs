// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StateReplay.Binary.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;

namespace StateReplay.Tests
{
    public partial class UT_StateReplay
    {
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
    }
}

