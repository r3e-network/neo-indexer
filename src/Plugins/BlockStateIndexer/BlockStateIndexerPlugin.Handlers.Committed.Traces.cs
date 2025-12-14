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
        private bool TryQueueTransactionResultsUpload(Block block, IReadOnlyCollection<ExecutionTraceRecorder> recorders, bool allowDatabaseUploads)
        {
            if (!allowDatabaseUploads || recorders.Count == 0)
                return true;

            return StateRecorderSupabase.TryQueueTransactionResultsUpload(block.Index, block.Hash.ToString(), recorders);
        }

        private (int Attempted, int Enqueued) TryQueueTraceUploads(Block block, IReadOnlyCollection<ExecutionTraceRecorder> recorders, bool allowDatabaseUploads)
        {
            if (!allowDatabaseUploads || recorders.Count == 0)
                return (0, 0);

            var uploadTraces = block.Transactions.Length >= Settings.Default.MinTransactionCount;

            if (!uploadTraces)
            {
                Utility.Log(Name, LogLevel.Debug,
                    $"Block {block.Index}: Skipping trace upload (tx count {block.Transactions.Length} below minimum {Settings.Default.MinTransactionCount}).");
                return (0, 0);
            }

            var attempted = 0;
            var enqueued = 0;
            foreach (var recorder in recorders)
            {
                if (recorder is null || !recorder.HasTraces)
                    continue;

                attempted++;
                if (StateRecorderSupabase.TryQueueTraceUpload(block.Index, block.Hash.ToString(), recorder))
                    enqueued++;
            }

            return (attempted, enqueued);
        }
    }
}
