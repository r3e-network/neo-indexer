// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BinaryFormatReader.Models.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo;
using System.IO;
using System.Text;

namespace StateReplay.Tests
{
    public partial class UT_BinaryFormatReader
    {
        [TestMethod]
        public void BinaryStateEntry_PropertiesAreCorrect()
        {
            var contractHash = UInt160.Zero;
            var key = new byte[] { 0x01, 0x02, 0x03 };
            var value = new byte[] { 0x04, 0x05 };
            var readOrder = 42;

            var entry = new BinaryStateEntry(contractHash, key, value, readOrder);

            Assert.AreEqual(contractHash, entry.ContractHash);
            CollectionAssert.AreEqual(key, entry.Key);
            CollectionAssert.AreEqual(value, entry.Value);
            Assert.AreEqual(readOrder, entry.ReadOrder);
        }

        [TestMethod]
        public void BinaryStateFile_PropertiesAreCorrect()
        {
            var blockIndex = 12345u;
            var entries = new[] { new BinaryStateEntry(UInt160.Zero, new byte[1], new byte[1], 0) };

            var file = new BinaryStateFile(blockIndex, entries);

            Assert.AreEqual(blockIndex, file.BlockIndex);
            Assert.AreEqual(1, file.Entries.Count);
        }

        [TestMethod]
        public void Read_EntryWithCustomContractHash_ParsesCorrectly()
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
            writer.Write(1u);

            // Entry count
            writer.Write(1);

            // Custom contract hash (20 bytes - all 0xFF)
            var customHash = new byte[20];
            for (int i = 0; i < 20; i++) customHash[i] = 0xFF;
            writer.Write(customHash);

            // Key
            var key = new byte[] { 0xAB, 0xCD };
            writer.Write((ushort)key.Length);
            writer.Write(key);

            // Value
            var value = new byte[] { 0xEF };
            writer.Write(value.Length);
            writer.Write(value);

            // ReadOrder
            writer.Write(99);

            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, ms.ToArray());

            var result = BinaryFormatReader.Read(path);

            Assert.AreEqual(1, result.Entries.Count);
            var entry = result.Entries[0];
            CollectionAssert.AreEqual(customHash, entry.ContractHash.GetSpan().ToArray());
            CollectionAssert.AreEqual(key, entry.Key);
            CollectionAssert.AreEqual(value, entry.Value);
            Assert.AreEqual(99, entry.ReadOrder);
        }
    }
}

