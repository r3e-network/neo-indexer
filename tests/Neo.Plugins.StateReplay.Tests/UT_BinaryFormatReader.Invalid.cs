// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BinaryFormatReader.Invalid.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace StateReplay.Tests
{
    public partial class UT_BinaryFormatReader
    {
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
    }
}

