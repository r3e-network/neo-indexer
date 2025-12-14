// Copyright (C) 2015-2025 The Neo Project.
//
// UT_BinaryFormatReader.IsBinaryFormat.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace StateReplay.Tests
{
    public partial class UT_BinaryFormatReader
    {
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
    }
}

