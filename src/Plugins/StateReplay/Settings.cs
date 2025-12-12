// Copyright (C) 2015-2025 The Neo Project.
//
// Settings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;

namespace StateReplay
{
    internal sealed class Settings
    {
        /// <summary>
        /// Whether the plugin is enabled.
        /// </summary>
        public bool Enabled { get; private set; } = true;

        /// <summary>
        /// Supabase URL for downloading binary files.
        /// </summary>
        public string SupabaseUrl { get; private set; } = string.Empty;

        /// <summary>
        /// Supabase API key.
        /// </summary>
        public string SupabaseApiKey { get; private set; } = string.Empty;

        /// <summary>
        /// Supabase storage bucket name.
        /// </summary>
        public string SupabaseBucket { get; private set; } = "block-state";

        /// <summary>
        /// Local cache directory for downloaded binary files.
        /// </summary>
        public string CacheDirectory { get; private set; } = "state-replay-cache";

        /// <summary>
        /// Whether to enable comparison mode by default.
        /// </summary>
        public bool ComparisonModeEnabled { get; private set; } = false;

        public static Settings Default { get; private set; } = new Settings();

        private Settings() { }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings
            {
                Enabled = section.GetValue(nameof(Enabled), true),
                SupabaseUrl = section.GetValue(nameof(SupabaseUrl), string.Empty) ?? string.Empty,
                SupabaseApiKey = section.GetValue(nameof(SupabaseApiKey), string.Empty) ?? string.Empty,
                SupabaseBucket = section.GetValue(nameof(SupabaseBucket), "block-state") ?? "block-state",
                CacheDirectory = section.GetValue(nameof(CacheDirectory), "state-replay-cache") ?? "state-replay-cache",
                ComparisonModeEnabled = section.GetValue(nameof(ComparisonModeEnabled), false)
            };
        }
    }
}
