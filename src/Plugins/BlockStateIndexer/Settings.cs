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
using Neo.Persistence;

namespace Neo.Plugins.BlockStateIndexer
{
    internal sealed class Settings
    {
        /// <summary>
        /// Whether the plugin is enabled.
        /// </summary>
        public bool Enabled { get; private set; } = true;

        /// <summary>
        /// Network ID to filter blocks (0 = all networks).
        /// </summary>
        public uint Network { get; private set; } = 0;

        /// <summary>
        /// Minimum transaction count required to record block state.
        /// Blocks with fewer transactions will be skipped.
        /// </summary>
        public int MinTransactionCount { get; private set; } = 1;

        /// <summary>
        /// Upload mode: Binary, RestApi, or Both (Postgres treated as RestApi for compatibility).
        /// </summary>
        public StateRecorderSettings.UploadMode UploadMode { get; private set; } = StateRecorderSettings.UploadMode.Both;

        /// <summary>
        /// Exception handling policy.
        /// </summary>
        public UnhandledExceptionPolicy ExceptionPolicy { get; private set; } = UnhandledExceptionPolicy.StopPlugin;

        public static Settings Default { get; private set; } = new Settings();

        private Settings() { }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings
            {
                Enabled = section.GetValue(nameof(Enabled), true),
                Network = section.GetValue(nameof(Network), 0u),
                MinTransactionCount = section.GetValue(nameof(MinTransactionCount), 1),
                UploadMode = section.GetValue(nameof(UploadMode), StateRecorderSettings.UploadMode.Both),
                ExceptionPolicy = section.GetValue(nameof(ExceptionPolicy), UnhandledExceptionPolicy.StopPlugin)
            };
        }
    }
}
