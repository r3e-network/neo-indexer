// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.Commands.Supabase.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using System;

namespace StateReplay
{
    public partial class StateReplayPlugin
    {
        [ConsoleCommand("replay supabase", Category = "Replay", Description = "Replay a block by fetching storage_reads from Supabase Postgres")]
        internal void ReplayBlockStateFromSupabase(uint blockIndex)
        {
            if (_system is null)
                throw new InvalidOperationException("NeoSystem is not ready.");

            if (string.IsNullOrEmpty(Settings.Default.SupabaseUrl) || string.IsNullOrEmpty(Settings.Default.SupabaseApiKey))
            {
                ConsoleHelper.Error("Supabase not configured. Set SupabaseUrl and SupabaseApiKey in StateReplay.json");
                return;
            }

            try
            {
                ReplayFromSupabaseAsync(blockIndex).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                ConsoleHelper.Error($"Replay failed: {ex.Message}");
            }
        }

        [ConsoleCommand("replay download", Category = "Replay", Description = "Download binary state file from Supabase")]
        internal void DownloadBlockState(uint blockIndex)
        {
            if (string.IsNullOrEmpty(Settings.Default.SupabaseUrl) || string.IsNullOrEmpty(Settings.Default.SupabaseApiKey))
            {
                ConsoleHelper.Error("Supabase not configured. Set SupabaseUrl and SupabaseApiKey in StateReplay.json");
                return;
            }

            var fileName = $"block-{blockIndex}.bin";
            var localPath = System.IO.Path.Combine(Settings.Default.CacheDirectory, fileName);

            try
            {
                DownloadFromSupabaseAsync(blockIndex, localPath).GetAwaiter().GetResult();
                ConsoleHelper.Info("Replay", $"Downloaded block {blockIndex} to {localPath}");
            }
            catch (Exception ex)
            {
                ConsoleHelper.Error($"Download failed: {ex.Message}");
            }
        }
    }
}
