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
using Neo.Persistence;
using Neo.SmartContract;
using Neo.Plugins;
using System;
using static System.IO.Path;

namespace Neo.Plugins.BlockStateIndexer
{
    /// <summary>
    /// Plugin that captures all storage state reads during block execution
    /// and uploads them to Supabase for analysis and replay.
    /// </summary>
    public sealed partial class BlockStateIndexerPlugin : Plugin, ICommittingHandler, ICommittedHandler
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
    }
}
