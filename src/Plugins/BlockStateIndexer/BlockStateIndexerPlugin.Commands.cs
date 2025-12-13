// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexerPlugin.Commands.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.ConsoleService;
using Neo.Persistence;

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class BlockStateIndexerPlugin
    {
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
            var queueStats = StateRecorderSupabase.GetUploadQueueStats();
            ConsoleHelper.Info($"  - UploadQueue Pending: high={queueStats.PendingHighPriority}, low={queueStats.PendingLowPriority}");
            ConsoleHelper.Info($"  - UploadQueue Dropped: high={queueStats.DroppedHighPriority}, low={queueStats.DroppedLowPriority}");
        }
    }
}

