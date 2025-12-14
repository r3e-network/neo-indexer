// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Queueing.cs file belongs to the neo project and is free
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
        /// <summary>
        /// Queue a per-transaction upload (tx result always; traces optionally). Returns false if the queue is full and the work was dropped.
        /// </summary>
        public static bool TryQueueTransactionUpload(uint blockIndex, string blockHash, ExecutionTraceRecorder recorder, bool uploadTraces)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));

            var txHash = recorder.TxHash?.ToString();
            if (string.IsNullOrWhiteSpace(txHash))
                return true; // Nothing to do without a transaction hash.

            return UploadQueue.TryEnqueueLow(
                blockIndex,
                uploadTraces ? $"tx upload (traces, tx={txHash})" : $"tx upload (result-only, tx={txHash})",
                () => ExecuteWithRetryAsync(
                    () => ExecuteIfCanonicalAsync(
                        blockIndex,
                        blockHash,
                        "tx upload",
                        () => UploadTransactionAsync(blockIndex, blockHash, txHash, recorder, uploadTraces)),
                    "tx upload",
                    blockIndex));
        }

        /// <summary>
         /// Queue a transaction trace upload (low priority). Returns false if the queue is full and the work was dropped.
         /// </summary>
        public static bool TryQueueTraceUpload(uint blockIndex, ExecutionTraceRecorder recorder)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));
            if (!recorder.HasTraces) return true;

            var txHash = recorder.TxHash?.ToString() ?? "(unknown)";
            return UploadQueue.TryEnqueueLow(
                blockIndex,
                $"trace upload (tx={txHash})",
                () => ExecuteWithRetryAsync(
                    () => UploadBlockTraceAsync(blockIndex, recorder),
                    "trace upload",
                    blockIndex));
        }

        /// <summary>
        /// Queue a transaction trace upload (low priority) with an expected block hash for reorg-safety.
        /// </summary>
        public static bool TryQueueTraceUpload(uint blockIndex, string blockHash, ExecutionTraceRecorder recorder)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));
            if (!recorder.HasTraces) return true;

            var txHash = recorder.TxHash?.ToString() ?? "(unknown)";
            return UploadQueue.TryEnqueueLow(
                blockIndex,
                $"trace upload (tx={txHash})",
                () => ExecuteWithRetryAsync(
                    () => ExecuteIfCanonicalAsync(
                        blockIndex,
                        blockHash,
                        "trace upload",
                        () => UploadBlockTraceAsync(blockIndex, blockHash, recorder)),
                    "trace upload",
                    blockIndex));
        }

        /// <summary>
        /// Queue a block stats upload (high priority). Returns false if the queue is full and the work was dropped.
        /// </summary>
        public static bool TryQueueBlockStatsUpload(BlockStats stats)
        {
            if (stats is null) throw new ArgumentNullException(nameof(stats));

            return UploadQueue.TryEnqueueHigh(
                stats.BlockIndex,
                "block stats upload",
                () => ExecuteWithRetryAsync(
                    () => UploadBlockStatsAsync(stats),
                    "block stats upload",
                    stats.BlockIndex));
        }

        /// <summary>
        /// Queue a block stats upload (high priority) with an expected block hash for reorg-safety.
        /// </summary>
        public static bool TryQueueBlockStatsUpload(BlockStats stats, string blockHash)
        {
            if (stats is null) throw new ArgumentNullException(nameof(stats));

            return UploadQueue.TryEnqueueHigh(
                stats.BlockIndex,
                "block stats upload",
                () => ExecuteWithRetryAsync(
                    () => ExecuteIfCanonicalAsync(
                        stats.BlockIndex,
                        blockHash,
                        "block stats upload",
                        () => UploadBlockStatsAsync(stats, blockHash)),
                    "block stats upload",
                    stats.BlockIndex));
        }
    }
}
