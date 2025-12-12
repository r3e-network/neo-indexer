// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BinaryFormatReader.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo;
using System;
using System.IO;
using System.Text;

namespace StateReplay.Tests
{
    [TestClass]
    public class UT_BinaryFormatReader
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

            // Entries
            for (int i = 0; i < entryCount; i++)
            {
                // ContractHash (20 bytes)
                writer.Write(new byte[20]);

                // Key length and key
                var keyBytes = new byte[] { 0x01, 0x02 };
                writer.Write((ushort)keyBytes.Length);
                writer.Write(keyBytes);

                // Value length and value
                var valueBytes = new byte[] { 0x03, 0x04, 0x05 };
                writer.Write(valueBytes.Length);
                writer.Write(valueBytes);

                // ReadOrder
                writer.Write(i);
            }

            return ms.ToArray();
        }

        [TestMethod]
        public void Read_ValidFile_ReturnsCorrectBlockIndex()
        {
            var bytes = CreateValidBinaryFile(12345u);
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, bytes);

            var result = BinaryFormatReader.Read(path);

            Assert.AreEqual(12345u, result.BlockIndex);
            Assert.AreEqual(0, result.Entries.Count);
        }

        [TestMethod]
        public void Read_ValidFileWithEntries_ParsesEntriesCorrectly()
        {
            var bytes = CreateValidBinaryFile(100u, 3);
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, bytes);

            var result = BinaryFormatReader.Read(path);

            Assert.AreEqual(100u, result.BlockIndex);
            Assert.AreEqual(3, result.Entries.Count);

            foreach (var entry in result.Entries)
            {
                Assert.IsNotNull(entry.ContractHash);
                Assert.IsNotNull(entry.Key);
                Assert.IsNotNull(entry.Value);
                Assert.AreEqual(2, entry.Key.Length);
                Assert.AreEqual(3, entry.Value.Length);
            }
        }

        [TestMethod]
        public void Read_FromStream_WorksCorrectly()
        {
            var bytes = CreateValidBinaryFile(999u, 1);

            using var stream = new MemoryStream(bytes);
            var result = BinaryFormatReader.Read(stream);

            Assert.AreEqual(999u, result.BlockIndex);
            Assert.AreEqual(1, result.Entries.Count);
        }

        [TestMethod]
        public void Read_InvalidMagic_ThrowsInvalidDataException()
        {
            var bytes = new byte[] { (byte)'X', (byte)'X', (byte)'X', (byte)'X', 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, bytes);

            Assert.ThrowsExactly<InvalidDataException>(() => BinaryFormatReader.Read(path));
        }

        [TestMethod]
        public void Read_UnsupportedVersion_ThrowsInvalidDataException()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // Magic
            writer.Write((byte)'N');
            writer.Write((byte)'S');
            writer.Write((byte)'B');
            writer.Write((byte)'R');

            // Invalid version
            writer.Write((ushort)99);

            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, ms.ToArray());

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => BinaryFormatReader.Read(path));
            Assert.IsTrue(ex.Message.Contains("Unsupported version"));
        }

        [TestMethod]
        public void Read_NegativeEntryCount_ThrowsInvalidDataException()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // Magic
            writer.Write((byte)'N');
            writer.Write((byte)'S');
            writer.Write((byte)'B');
            writer.Write((byte)'R');

            // Version
            writer.Write((ushort)1);

            // Block index
            writer.Write(0u);

            // Negative entry count
            writer.Write(-1);

            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, ms.ToArray());

            var ex = Assert.ThrowsExactly<InvalidDataException>(() => BinaryFormatReader.Read(path));
            Assert.IsTrue(ex.Message.Contains("Invalid entry count"));
        }

        [TestMethod]
        public void Read_TruncatedContractHash_ThrowsInvalidDataException()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            writer.Write((byte)'N');
            writer.Write((byte)'S');
            writer.Write((byte)'B');
            writer.Write((byte)'R');
            writer.Write((ushort)1);
            writer.Write(0u);
            writer.Write(1); // one entry

            // Only 10 bytes instead of 20 for contract hash
            writer.Write(new byte[10]);

            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, ms.ToArray());

            Assert.ThrowsExactly<InvalidDataException>(() => BinaryFormatReader.Read(path));
        }

        [TestMethod]
        public void Read_TruncatedKey_ThrowsInvalidDataException()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            writer.Write((byte)'N');
            writer.Write((byte)'S');
            writer.Write((byte)'B');
            writer.Write((byte)'R');
            writer.Write((ushort)1);
            writer.Write(0u);
            writer.Write(1); // one entry

            writer.Write(new byte[20]); // full contract hash
            writer.Write((ushort)5);    // key length says 5
            writer.Write(new byte[2]);  // but only 2 bytes provided

            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, ms.ToArray());

            Assert.ThrowsExactly<InvalidDataException>(() => BinaryFormatReader.Read(path));
        }

        [TestMethod]
        public void Read_TruncatedValue_ThrowsInvalidDataException()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            writer.Write((byte)'N');
            writer.Write((byte)'S');
            writer.Write((byte)'B');
            writer.Write((byte)'R');
            writer.Write((ushort)1);
            writer.Write(0u);
            writer.Write(1); // one entry

            writer.Write(new byte[20]); // full contract hash
            writer.Write((ushort)2);
            writer.Write(new byte[] { 0x01, 0x02 });
            writer.Write(4);            // value length says 4
            writer.Write(new byte[1]);  // but only 1 byte provided

            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, ms.ToArray());

            Assert.ThrowsExactly<InvalidDataException>(() => BinaryFormatReader.Read(path));
        }

        [TestMethod]
        public void IsBinaryFormat_ValidFile_ReturnsTrue()
        {
            var bytes = CreateValidBinaryFile(1u);
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, bytes);

            Assert.IsTrue(BinaryFormatReader.IsBinaryFormat(path));
        }

        [TestMethod]
        public void IsBinaryFormat_NonExistentFile_ReturnsFalse()
        {
            Assert.IsFalse(BinaryFormatReader.IsBinaryFormat("/nonexistent/path/file.bin"));
        }

        [TestMethod]
        public void IsBinaryFormat_TooShortFile_ReturnsFalse()
        {
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, new byte[] { 0x01, 0x02 });

            Assert.IsFalse(BinaryFormatReader.IsBinaryFormat(path));
        }

        [TestMethod]
        public void IsBinaryFormat_WrongMagic_ReturnsFalse()
        {
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, new byte[] { (byte)'J', (byte)'S', (byte)'O', (byte)'N' });

            Assert.IsFalse(BinaryFormatReader.IsBinaryFormat(path));
        }

        [TestMethod]
        public void IsBinaryFormat_JsonFile_ReturnsFalse()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "{\"block\": 0}");

            Assert.IsFalse(BinaryFormatReader.IsBinaryFormat(path));
        }

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
