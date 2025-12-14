// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RecordBuilders.StorageReads.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Generic;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static List<StorageReadRecord> BuildStorageReadRecords(BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
            var blockIndex = checked((int)recorder.BlockIndex);
            var reads = new List<StorageReadRecord>(entries.Length);
            foreach (var entry in entries)
            {
                var contractId = entry.Key.Id;
                var keyBase64 = Convert.ToBase64String(entry.Key.Key.Span);
                var valueBase64 = Convert.ToBase64String(entry.Value.Value.Span);
                reads.Add(new StorageReadRecord(
                    blockIndex,
                    contractId,
                    keyBase64,
                    valueBase64,
                    entry.Order,
                    entry.TxHash?.ToString(),
                    entry.Source));
            }
            return reads;
        }
    }
}

