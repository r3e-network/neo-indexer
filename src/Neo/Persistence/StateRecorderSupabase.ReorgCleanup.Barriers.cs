// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.ReorgCleanup.Barriers.cs file belongs to the neo project and is free
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
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private sealed record ReorgCleanupBarrier(string BlockHash, TaskCompletionSource<object?> Completion);

        private static readonly ConcurrentDictionary<uint, ReorgCleanupBarrier> ReorgCleanupBarriers = new();

        private static Task WaitForReorgCleanupAsync(uint blockIndex, string expectedBlockHash)
        {
            if (string.IsNullOrWhiteSpace(expectedBlockHash))
                return Task.CompletedTask;

            if (ReorgCleanupBarriers.TryGetValue(blockIndex, out var barrier) &&
                string.Equals(barrier.BlockHash, expectedBlockHash, StringComparison.Ordinal))
            {
                return barrier.Completion.Task;
            }

            return Task.CompletedTask;
        }

        private static ReorgCleanupBarrier GetOrReplaceReorgBarrier(uint blockIndex, string expectedBlockHash)
        {
            while (true)
            {
                if (ReorgCleanupBarriers.TryGetValue(blockIndex, out var existing))
                {
                    if (string.Equals(existing.BlockHash, expectedBlockHash, StringComparison.Ordinal))
                        return existing;

                    var created = new ReorgCleanupBarrier(
                        expectedBlockHash,
                        new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously));

                    if (ReorgCleanupBarriers.TryUpdate(blockIndex, created, existing))
                    {
                        // Release anyone waiting on the previous barrier.
                        existing.Completion.TrySetResult(null);
                        return created;
                    }

                    continue;
                }

                var fresh = new ReorgCleanupBarrier(
                    expectedBlockHash,
                    new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously));

                if (ReorgCleanupBarriers.TryAdd(blockIndex, fresh))
                    return fresh;
            }
        }

        private static void TryRemoveReorgBarrier(uint blockIndex, string expectedBlockHash)
        {
            if (ReorgCleanupBarriers.TryGetValue(blockIndex, out var existing) &&
                string.Equals(existing.BlockHash, expectedBlockHash, StringComparison.Ordinal) &&
                existing.Completion.Task.IsCompleted)
            {
                ReorgCleanupBarriers.TryRemove(blockIndex, out _);
            }
        }
    }
}

