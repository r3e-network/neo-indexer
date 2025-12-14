// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.ReorgGuard.CanonicalHashes.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Concurrent;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private const int CanonicalBlockHashWindow = 10_000;

        private static readonly ConcurrentDictionary<uint, string> CanonicalBlockHashes = new();
        private static readonly ConcurrentQueue<(uint BlockIndex, string BlockHash)> CanonicalBlockHashHistory = new();
        private static long _maxBlockIndexSeen = -1;

        private static void NoteBlockIndexSeen(uint blockIndex)
        {
            while (true)
            {
                var current = System.Threading.Interlocked.Read(ref _maxBlockIndexSeen);
                if (current >= blockIndex)
                    return;

                if (System.Threading.Interlocked.CompareExchange(ref _maxBlockIndexSeen, blockIndex, current) == current)
                    return;
            }
        }

        private static bool TryGetCanonicalBlockHash(uint blockIndex, out string? blockHash)
        {
            return CanonicalBlockHashes.TryGetValue(blockIndex, out blockHash);
        }

        private static (bool HadPrevious, string? PreviousHash) UpdateCanonicalBlockHash(uint blockIndex, string blockHash)
        {
            NoteBlockIndexSeen(blockIndex);

            var hadPrevious = CanonicalBlockHashes.TryGetValue(blockIndex, out var previous);
            CanonicalBlockHashes[blockIndex] = blockHash;
            CanonicalBlockHashHistory.Enqueue((blockIndex, blockHash));

            PruneCanonicalHashes();

            return (hadPrevious, previous);
        }

        private static void PruneCanonicalHashes()
        {
            var max = System.Threading.Interlocked.Read(ref _maxBlockIndexSeen);
            if (max <= CanonicalBlockHashWindow)
                return;

            var threshold = (uint)(max - CanonicalBlockHashWindow);

            while (CanonicalBlockHashHistory.TryPeek(out var old) && old.BlockIndex < threshold)
            {
                if (!CanonicalBlockHashHistory.TryDequeue(out old))
                    break;

                if (CanonicalBlockHashes.TryGetValue(old.BlockIndex, out var current) &&
                    string.Equals(current, old.BlockHash, StringComparison.Ordinal))
                {
                    CanonicalBlockHashes.TryRemove(old.BlockIndex, out _);
                }
            }
        }
    }
}

