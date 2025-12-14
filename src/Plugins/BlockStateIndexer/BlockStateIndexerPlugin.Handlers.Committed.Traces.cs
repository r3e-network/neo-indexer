// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexerPlugin.Handlers.Committed.Traces.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System.Collections.Generic;

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class BlockStateIndexerPlugin
    {
        private void TryQueueTraceUploads(Block block, IReadOnlyCollection<ExecutionTraceRecorder> recorders, bool allowRestApiUploads)
        {
            if (allowRestApiUploads &&
                block.Transactions.Length >= Settings.Default.MinTransactionCount &&
                recorders.Count > 0)
            {
                foreach (var recorder in recorders)
                {
                    StateRecorderSupabase.TryQueueTraceUpload(block.Index, recorder);
                }
            }
            else if (recorders.Count > 0 && block.Transactions.Length < Settings.Default.MinTransactionCount)
            {
                Utility.Log(Name, LogLevel.Debug,
                    $"Block {block.Index}: Skipping trace upload (tx count {block.Transactions.Length} below minimum {Settings.Default.MinTransactionCount})");
            }
        }
    }
}
