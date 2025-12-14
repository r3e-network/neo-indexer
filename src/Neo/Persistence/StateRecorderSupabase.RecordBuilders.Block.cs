// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RecordBuilders.Block.cs file belongs to the neo project and is free
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
        private static BlockRecord BuildBlockRecord(BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
            var timestamp = recorder.Timestamp <= long.MaxValue ? (long)recorder.Timestamp : long.MaxValue;
            return new BlockRecord(
                checked((int)recorder.BlockIndex),
                recorder.BlockHash.ToString(),
                timestamp,
                recorder.TransactionCount,
                entries.Length);
        }
    }
}

