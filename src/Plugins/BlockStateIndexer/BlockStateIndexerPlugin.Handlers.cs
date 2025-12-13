// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexerPlugin.Handlers.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.IEventHandlers;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System.Collections.Generic;

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class BlockStateIndexerPlugin
    {
        /// <summary>
        /// Called when a block is about to be committed.
        /// Starts recording state reads if the block meets criteria.
        /// </summary>
        void ICommittingHandler.Blockchain_Committing_Handler(
            global::Neo.NeoSystem system,
            Block block,
            DataCache snapshot,
            IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            // Read/trace recorders are started via TracingApplicationEngineProvider on OnPersist.
            return;
        }

        /// <summary>
        /// Called after a block has been committed.
        /// Triggers upload of recorded state reads.
        /// </summary>
        void ICommittedHandler.Blockchain_Committed_Handler(global::Neo.NeoSystem system, Block block)
        {
            if (!Settings.Default.Enabled) return;
            if (Settings.Default.Network != 0 && system.Settings.Network != Settings.Default.Network) return;

            var provider = _tracingProvider;
            if (provider == null) return;

            var recorderSettings = StateRecorderSettings.Current;
            var pluginMode = Settings.Default.UploadMode;

            var pluginAllowsBinary = pluginMode is StateRecorderSettings.UploadMode.Binary or StateRecorderSettings.UploadMode.Both;
            var pluginAllowsRestApi = pluginMode is StateRecorderSettings.UploadMode.RestApi
                or StateRecorderSettings.UploadMode.Postgres
                or StateRecorderSettings.UploadMode.Both;

            var envAllowsBinary = recorderSettings.Mode is StateRecorderSettings.UploadMode.Binary or StateRecorderSettings.UploadMode.Both;
            var envAllowsRestApi = recorderSettings.Mode is StateRecorderSettings.UploadMode.RestApi
                or StateRecorderSettings.UploadMode.Postgres
                or StateRecorderSettings.UploadMode.Both;

            var allowBinaryUploads = pluginAllowsBinary && envAllowsBinary;
            var allowRestApiUploads = pluginAllowsRestApi && envAllowsRestApi;

            var readRecorder = provider.DrainReadRecorder(block.Index);
            var storageReadCount = readRecorder?.Entries.Count ?? 0;

            if (readRecorder != null && (allowBinaryUploads || allowRestApiUploads))
            {
                if (storageReadCount > 0)
                {
                    var effectiveReadMode =
                        allowBinaryUploads && allowRestApiUploads
                            ? StateRecorderSettings.UploadMode.Both
                            : allowBinaryUploads
                                ? StateRecorderSettings.UploadMode.Binary
                                : StateRecorderSettings.UploadMode.RestApi;

                    StateRecorderSupabase.TryUpload(readRecorder, effectiveReadMode);
                }
                else if (allowRestApiUploads)
                {
                    // Still upsert the block row (read_key_count=0) so the frontend can
                    // search blocks even when no storage keys were touched. Avoid binary
                    // snapshot uploads for empty read sets to prevent file explosion.
                    StateRecorderSupabase.TryUpload(readRecorder, StateRecorderSettings.UploadMode.RestApi);
                }
            }

            var recorders = provider.DrainBlock(block.Index);
            if (recorders.Count == 0 && readRecorder == null) return;

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

            long totalGasConsumed = 0;
            int opCodeCount = 0;
            int syscallCount = 0;
            int contractCallCount = 0;
            int storageWriteCount = 0;
            int notificationCount = 0;

            foreach (var recorder in recorders)
            {
                var txStats = recorder.GetStats();
                totalGasConsumed += txStats.TotalGasConsumed;
                opCodeCount += txStats.OpCodeCount;
                syscallCount += txStats.SyscallCount;
                contractCallCount += txStats.ContractCallCount;
                storageWriteCount += txStats.StorageWriteCount;
                notificationCount += txStats.NotificationCount;
            }

            var blockStats = new BlockStats
            {
                BlockIndex = block.Index,
                TransactionCount = block.Transactions.Length,
                TotalGasConsumed = totalGasConsumed,
                OpCodeCount = opCodeCount,
                SyscallCount = syscallCount,
                ContractCallCount = contractCallCount,
                StorageReadCount = storageReadCount,
                StorageWriteCount = storageWriteCount,
                NotificationCount = notificationCount
            };

            if (allowRestApiUploads)
            {
                StateRecorderSupabase.TryQueueBlockStatsUpload(blockStats);
            }

            Utility.Log(Name, LogLevel.Info,
                $"Block {block.Index}: Queued uploads (reads={storageReadCount}, traces={recorders.Count})");
        }
    }
}

