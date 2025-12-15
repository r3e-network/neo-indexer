// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexerPlugin.Handlers.Committed.cs file belongs to the neo project and is free
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
        private static IReadOnlyDictionary<UInt256, int>? TryBuildStorageReadCountsByTransaction(BlockReadRecorder recorder)
        {
            Dictionary<UInt256, int>? counts = null;

            foreach (var entry in recorder.Entries)
            {
                if (entry.TxHash is null)
                    continue;

                counts ??= new Dictionary<UInt256, int>();
                var txHash = entry.TxHash;
                counts[txHash] = counts.TryGetValue(txHash, out var current) ? current + 1 : 1;
            }

            return counts;
        }

        private void HandleCommitted(global::Neo.NeoSystem system, Block block)
        {
            if (!Settings.Default.Enabled) return;
            if (Settings.Default.Network != 0 && system.Settings.Network != Settings.Default.Network) return;

            var provider = _tracingProvider;
            if (provider == null) return;

            var recorderSettings = StateRecorderSettings.Current;
            var (allowBinaryUploads, allowDatabaseUploads) = ResolveUploadAllows(recorderSettings);

            var readRecorder = provider.DrainReadRecorder(block.Index);
            var storageReadCount = readRecorder?.Entries.Count ?? 0;
            var storageReadCountsByTransaction = readRecorder is null
                ? null
                : TryBuildStorageReadCountsByTransaction(readRecorder);
            if (readRecorder != null)
                TryUploadStorageReads(readRecorder, recorderSettings, allowBinaryUploads, allowDatabaseUploads, storageReadCount);

            var recorders = provider.DrainBlock(block.Index);
            if (recorders.Count == 0 && readRecorder == null) return;
            var txResultsEnqueued = TryQueueTransactionResultsUpload(block, recorders, storageReadCountsByTransaction, allowDatabaseUploads);
            var (traceAttempted, traceEnqueued) = TryQueueTraceUploads(block, recorders, allowDatabaseUploads);

            var blockStats = BuildBlockStats(block, recorders, storageReadCount);
            if (allowDatabaseUploads)
            {
                StateRecorderSupabase.TryQueueBlockStatsUpload(blockStats, block.Hash.ToString());
            }

            var txResultsLabel = !allowDatabaseUploads || recorders.Count == 0
                ? "0"
                : txResultsEnqueued
                    ? recorders.Count.ToString()
                    : $"{recorders.Count} dropped";

            var tracesLabel = traceAttempted == 0
                ? "0"
                : $"{traceEnqueued}/{traceAttempted}";

            Utility.Log(Name, LogLevel.Info,
                $"Block {block.Index}: Queued uploads (reads={storageReadCount}, tx_results={txResultsLabel}, traces={tracesLabel})");
        }
    }
}
