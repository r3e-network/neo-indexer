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
            var capacity = EstimateBinaryPayloadCapacity(entries);

            using var stream = capacity > 0 ? new MemoryStream(capacity) : new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            WriteBinaryPayload(writer, recorder, entries);

            writer.Flush();
            return (stream.ToArray(), $"block-{recorder.BlockIndex}.bin");
        }
    }
}
