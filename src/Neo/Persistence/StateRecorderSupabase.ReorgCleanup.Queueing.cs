// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.ReorgCleanup.Queueing.cs file belongs to the neo project and is free
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
        private static void TryQueueReorgCleanup(
            uint blockIndex,
            string blockHash,
            StateRecorderSettings settings)
        {
            var barrier = GetOrReplaceReorgBarrier(blockIndex, blockHash);

            // Run as HIGH priority and force all other uploads for this block index to await completion.
            var enqueued = UploadQueue.TryEnqueueHigh(
                blockIndex,
                "reorg cleanup",
                async () =>
                {
                    try
                    {
                        await ExecuteWithRetryAsync(
                            () => DeleteBlockDataIfCanonicalAsync(blockIndex, blockHash, settings),
                            "reorg cleanup",
                            blockIndex).ConfigureAwait(false);
                    }
                    finally
                    {
                        barrier.Completion.TrySetResult(null);
                        TryRemoveReorgBarrier(blockIndex, blockHash);
                    }
                });

            if (!enqueued)
            {
                barrier.Completion.TrySetResult(null);
                TryRemoveReorgBarrier(blockIndex, blockHash);
            }
        }
    }
}

