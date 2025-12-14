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

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class BlockStateIndexerPlugin
    {
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
            if (readRecorder != null)
                TryUploadStorageReads(readRecorder, recorderSettings, allowBinaryUploads, allowDatabaseUploads, storageReadCount);

            var recorders = provider.DrainBlock(block.Index);
            if (recorders.Count == 0 && readRecorder == null) return;
            var txResultsEnqueued = TryQueueTransactionResultsUpload(block, recorders, allowDatabaseUploads);
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
