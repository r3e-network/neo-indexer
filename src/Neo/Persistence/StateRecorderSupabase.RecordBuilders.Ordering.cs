// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RecordBuilders.Ordering.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static BlockReadEntry[] GetOrderedEntries(BlockReadRecorder recorder)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));

            var entries = recorder.Entries;
            if (entries.Count == 0)
                return Array.Empty<BlockReadEntry>();

            var snapshot = new BlockReadEntry[entries.Count];
            var index = 0;
            foreach (var entry in entries)
                snapshot[index++] = entry;

            for (var i = 1; i < snapshot.Length; i++)
            {
                if (snapshot[i].Order < snapshot[i - 1].Order)
                {
                    Array.Sort(snapshot, static (a, b) => a.Order.CompareTo(b.Order));
                    break;
                }
            }
            return snapshot;
        }
    }
}

