// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.IEventHandlers;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.Plugins;
using System;
using System.Threading.Tasks;
using static System.IO.Path;

namespace Neo.Plugins.BlockStateIndexer
{
    /// <summary>
    /// Plugin that captures all storage state reads during block execution
    /// and uploads them to Supabase for analysis and replay.
    /// </summary>
    public sealed class BlockStateIndexerPlugin : Plugin, ICommittingHandler, ICommittedHandler
    {
        private global::Neo.NeoSystem? _neoSystem;
        private TracingApplicationEngineProvider? _tracingProvider;
        private IApplicationEngineProvider? _previousEngineProvider;

        public override string Name => "BlockStateIndexer";
        public override string Description => "Captures block storage state reads for analysis and replay.";
        protected override UnhandledExceptionPolicy ExceptionPolicy => Settings.Default.ExceptionPolicy;
        public override string ConfigFile => Combine(RootPath, "BlockStateIndexer.json");

        public BlockStateIndexerPlugin()
        {
            Blockchain.Committing += ((ICommittingHandler)this).Blockchain_Committing_Handler;
            Blockchain.Committed += ((ICommittedHandler)this).Blockchain_Committed_Handler;
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(global::Neo.NeoSystem system)
        {
            if (Settings.Default.Network != 0 && system.Settings.Network != Settings.Default.Network)
            {
                ConsoleHelper.Info($"BlockStateIndexer: Skipping network {system.Settings.Network:X8} (configured for {Settings.Default.Network:X8})");
                return;
            }

            _neoSystem = system;
            ConsoleHelper.Info($"BlockStateIndexer: Loaded for network {system.Settings.Network:X8}");
            ConsoleHelper.Info($"  - MinTransactionCount: {Settings.Default.MinTransactionCount}");
            ConsoleHelper.Info($"  - UploadMode: {Settings.Default.UploadMode}");

            var recorderSettings = StateRecorderSettings.Current;
            ConsoleHelper.Info($"  - Recorder Enabled: {recorderSettings.Enabled}");
            ConsoleHelper.Info($"  - Recorder Upload Mode (env): {recorderSettings.Mode}");
            ConsoleHelper.Info($"  - MaxStorageReadsPerBlock: {(recorderSettings.MaxStorageReadsPerBlock <= 0 ? "(unlimited)" : recorderSettings.MaxStorageReadsPerBlock)}");

            if (recorderSettings.Enabled && !recorderSettings.UploadEnabled)
            {
                ConsoleHelper.Info("BlockStateIndexer: State recorder enabled but Supabase URL/KEY are missing; uploads are disabled.");
            }

            var pluginMode = Settings.Default.UploadMode;
            var pluginAllowsBinary = pluginMode is StateRecorderSettings.UploadMode.Binary or StateRecorderSettings.UploadMode.Both;
            var pluginAllowsRestApi = pluginMode is StateRecorderSettings.UploadMode.RestApi
                or StateRecorderSettings.UploadMode.Postgres
                or StateRecorderSettings.UploadMode.Both;

            var envAllowsBinary = recorderSettings.Mode is StateRecorderSettings.UploadMode.Binary or StateRecorderSettings.UploadMode.Both;
            var envAllowsRestApi = recorderSettings.Mode is StateRecorderSettings.UploadMode.RestApi
                or StateRecorderSettings.UploadMode.Postgres
                or StateRecorderSettings.UploadMode.Both;

            if (pluginAllowsRestApi && !envAllowsRestApi)
            {
                ConsoleHelper.Info(
                    $"BlockStateIndexer: Warning - plugin UploadMode {pluginMode} expects RestApi/Postgres uploads, " +
                    $"but env NEO_STATE_RECORDER__UPLOAD_MODE is {recorderSettings.Mode}. " +
                    "Set NEO_STATE_RECORDER__UPLOAD_MODE=RestApi or Both to enable database writes.");
            }

            if (pluginAllowsBinary && !envAllowsBinary)
            {
                ConsoleHelper.Info(
                    $"BlockStateIndexer: Note - binary snapshot uploads are disabled by env upload mode {recorderSettings.Mode}. " +
                    "Set NEO_STATE_RECORDER__UPLOAD_MODE=Binary or Both if replayable .bin files are needed.");
            }

            if (Settings.Default.Enabled && recorderSettings.Enabled)
            {
                _previousEngineProvider = ApplicationEngine.Provider;
                _tracingProvider = new TracingApplicationEngineProvider(traceLevel: recorderSettings.TraceLevel);
                ApplicationEngine.Provider = _tracingProvider;
                ConsoleHelper.Info($"BlockStateIndexer: Tracing provider registered (level={recorderSettings.TraceLevel}).");
            }
            else
            {
                ConsoleHelper.Info("BlockStateIndexer: Tracing provider not registered (state recorder disabled).");
            }
        }

        public override void Dispose()
        {
            Blockchain.Committing -= ((ICommittingHandler)this).Blockchain_Committing_Handler;
            Blockchain.Committed -= ((ICommittedHandler)this).Blockchain_Committed_Handler;

            if (_tracingProvider != null)
            {
                ApplicationEngine.Provider = _previousEngineProvider;
                _tracingProvider = null;
                _previousEngineProvider = null;
            }

            GC.SuppressFinalize(this);
        }

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
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await StateRecorderSupabase.UploadBlockTraceAsync(block.Index, recorder).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Utility.Log(Name, LogLevel.Warning,
                                $"Block {block.Index}: Trace upload failed for tx {recorder.TxHash}: {ex.Message}");
                        }
                    });
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
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await StateRecorderSupabase.UploadBlockStatsAsync(blockStats).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Utility.Log(Name, LogLevel.Warning,
                            $"Block {block.Index}: Block stats upload failed: {ex.Message}");
                    }
                });
            }

            Utility.Log(Name, LogLevel.Info,
                $"Block {block.Index}: Queued uploads (reads={storageReadCount}, traces={recorders.Count})");
        }

        [ConsoleCommand("blockstateindexer status", Category = "BlockStateIndexer", Description = "Show plugin status")]
        private void OnStatusCommand()
        {
            ConsoleHelper.Info("BlockStateIndexer Status:");
            ConsoleHelper.Info($"  - Enabled: {Settings.Default.Enabled}");
            ConsoleHelper.Info($"  - Network: {(Settings.Default.Network == 0 ? "All" : Settings.Default.Network.ToString("X8"))}");
            ConsoleHelper.Info($"  - MinTransactionCount: {Settings.Default.MinTransactionCount}");
            ConsoleHelper.Info($"  - UploadMode: {Settings.Default.UploadMode}");
            ConsoleHelper.Info($"  - StateRecorder Enabled: {StateRecorderSettings.Current.Enabled}");
            ConsoleHelper.Info($"  - Supabase URL: {(string.IsNullOrEmpty(StateRecorderSettings.Current.SupabaseUrl) ? "(not set)" : StateRecorderSettings.Current.SupabaseUrl)}");
            ConsoleHelper.Info($"  - MaxStorageReadsPerBlock: {(StateRecorderSettings.Current.MaxStorageReadsPerBlock <= 0 ? "(unlimited)" : StateRecorderSettings.Current.MaxStorageReadsPerBlock)}");
        }
    }
}
