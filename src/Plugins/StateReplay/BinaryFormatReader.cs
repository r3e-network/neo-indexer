// Copyright (C) 2015-2025 The Neo Project.
//
// BinaryFormatReader.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StateReplay
{
    /// <summary>
    /// Reads binary block state files in NSBR format.
    /// Format: [Magic: 4 bytes "NSBR"] [Version: 2 bytes] [Block Index: 4 bytes] [Entry Count: 4 bytes]
    /// Entries: [ContractHash: 20 bytes] [Key Length: 2 bytes] [Key: variable] [Value Length: 4 bytes] [Value: variable] [ReadOrder: 4 bytes]
    /// </summary>
    public static class BinaryFormatReader
    {
        private static readonly byte[] ExpectedMagic = [(byte)'N', (byte)'S', (byte)'B', (byte)'R'];
        private const ushort SupportedVersion = 1;

        /// <summary>
        /// Read binary state file and return entries.
        /// </summary>
        public static BinaryStateFile Read(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return Read(stream);
        }

        /// <summary>
        /// Read binary state file from stream.
        /// </summary>
        public static BinaryStateFile Read(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            // Read and validate magic
            var magic = reader.ReadBytes(4);
            if (!magic.AsSpan().SequenceEqual(ExpectedMagic))
            {
                throw new InvalidDataException($"Invalid magic bytes. Expected 'NSBR', got '{Encoding.ASCII.GetString(magic)}'.");
            }

            // Read version
            var version = reader.ReadUInt16();
            if (version != SupportedVersion)
            {
                throw new InvalidDataException($"Unsupported version {version}. Only version {SupportedVersion} is supported.");
            }

            // Read header
            var blockIndex = reader.ReadUInt32();
            var entryCount = reader.ReadInt32();

            if (entryCount < 0)
            {
                throw new InvalidDataException($"Invalid entry count: {entryCount}.");
            }

            if (stream.CanSeek)
            {
                // Minimum bytes per entry:
                // ContractHash(20) + KeyLength(2) + ValueLength(4) + ReadOrder(4) = 30 bytes
                const int minBytesPerEntry = 30;
                var remaining = stream.Length - stream.Position;
                var minRequired = (long)entryCount * minBytesPerEntry;
                if (minRequired > remaining)
                {
                    throw new InvalidDataException(
                        $"File truncated or corrupt: declared {entryCount} entries but only {remaining} bytes remain.");
                }
            }

            // Read entries
            var entries = new List<BinaryStateEntry>(entryCount);
            for (var i = 0; i < entryCount; i++)
            {
                // ContractHash: 20 bytes
                var contractHashBytes = reader.ReadBytes(20);
                if (contractHashBytes.Length != 20)
                {
                    throw new InvalidDataException("Unexpected end of file while reading contract hash.");
                }
                var contractHash = new UInt160(contractHashBytes);

                // Key
                var keyLength = reader.ReadUInt16();
                var keyBytes = reader.ReadBytes(keyLength);
                if (keyBytes.Length != keyLength)
                {
                    throw new InvalidDataException("Unexpected end of file while reading key bytes.");
                }

                // Value
                var valueLength = reader.ReadInt32();
                if (valueLength < 0)
                {
                    throw new InvalidDataException($"Invalid value length: {valueLength}.");
                }
                if (stream.CanSeek)
                {
                    var remainingValueBytes = stream.Length - stream.Position;
                    if (valueLength > remainingValueBytes)
                    {
                        throw new InvalidDataException("Unexpected end of file while reading value bytes.");
                    }
                }
                var valueBytes = reader.ReadBytes(valueLength);
                if (valueBytes.Length != valueLength)
                {
                    throw new InvalidDataException("Unexpected end of file while reading value bytes.");
                }

                // ReadOrder
                var readOrder = reader.ReadInt32();

                entries.Add(new BinaryStateEntry(contractHash, keyBytes, valueBytes, readOrder));
            }

            return new BinaryStateFile(blockIndex, entries);
        }

        /// <summary>
        /// Check if the file is a binary NSBR format (by reading magic bytes).
        /// </summary>
        public static bool IsBinaryFormat(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            using var stream = File.OpenRead(filePath);
            if (stream.Length < 4) return false;

            var magic = new byte[4];
            _ = stream.Read(magic, 0, 4);
            return magic.AsSpan().SequenceEqual(ExpectedMagic);
        }
    }
}
