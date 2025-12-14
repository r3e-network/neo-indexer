// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.BinaryUpload.PayloadBuilders.Write.cs file belongs to the neo project and is free
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

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static void WriteBinaryPayload(BinaryWriter writer, BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
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
        }
    }
}

