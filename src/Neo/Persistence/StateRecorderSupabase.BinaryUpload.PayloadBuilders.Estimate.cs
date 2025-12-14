// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.BinaryUpload.PayloadBuilders.Estimate.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static int EstimateBinaryPayloadCapacity(BlockReadEntry[] entries)
        {
            if (entries.Length == 0)
                return 0;

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
            return estimatedSize > int.MaxValue ? int.MaxValue : (int)estimatedSize;
        }
    }
}

