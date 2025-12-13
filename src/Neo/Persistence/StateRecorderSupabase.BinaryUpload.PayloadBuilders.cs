// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.BinaryUpload.PayloadBuilders.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        /// <summary>
        /// Build binary payload according to spec:
        /// Header: [Magic: 4 bytes "NSBR"] [Version: 2 bytes] [Block Index: 4 bytes] [Entry Count: 4 bytes]
        /// Entries: Array of [ContractHash: 20 bytes] [Key Length: 2 bytes] [Key: variable] [Value Length: 4 bytes] [Value: variable] [ReadOrder: 4 bytes]
        /// </summary>
        private static (byte[] Buffer, string Path) BuildBinaryPayload(BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
            var capacity = 0;
            if (entries.Length > 0)
            {
                // Pre-size the MemoryStream to avoid repeated growth/copies on large blocks.
                long estimatedSize = BinaryMagic.Length + sizeof(ushort) + sizeof(uint) + sizeof(int);
                foreach (var entry in entries)
                {
                    estimatedSize += UInt160.Length; // ContractHash
                    var keyLength = sizeof(int) + entry.Key.Key.Length;
                    estimatedSize += sizeof(ushort) + keyLength;
                    estimatedSize += sizeof(int) + entry.Value.Value.Length;
                    estimatedSize += sizeof(int); // ReadOrder
                }
                capacity = estimatedSize > int.MaxValue ? int.MaxValue : (int)estimatedSize;
            }

            using var stream = capacity > 0 ? new MemoryStream(capacity) : new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            Span<byte> contractIdBuffer = stackalloc byte[sizeof(int)];

            // Header
            writer.Write(BinaryMagic);
            writer.Write(BinaryFormatVersion);
            writer.Write(recorder.BlockIndex);
            writer.Write(entries.Length);

            // Entries
            foreach (var entry in entries)
            {
                // ContractHash: 20 bytes
                writer.Write(entry.ContractHash.GetSpan());

                // Key
                var keyLength = sizeof(int) + entry.Key.Key.Length;
                if (keyLength > ushort.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"Key length {keyLength} exceeds max {ushort.MaxValue} for contract {entry.Key.Id}.");
                }
                writer.Write((ushort)keyLength);
                BinaryPrimitives.WriteInt32LittleEndian(contractIdBuffer, entry.Key.Id);
                writer.Write(contractIdBuffer);
                writer.Write(entry.Key.Key.Span);

                // Value
                var valueBytes = entry.Value.Value.Span;
                writer.Write(valueBytes.Length);
                writer.Write(valueBytes);

                // ReadOrder
                writer.Write(entry.Order);
            }

            writer.Flush();
            return (stream.ToArray(), $"block-{recorder.BlockIndex}.bin");
        }
    }
}

