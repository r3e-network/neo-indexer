// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.ConsoleService;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.VM;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using static System.IO.Path;

namespace StateReplay
{
    public partial class StateReplayPlugin : Plugin
    {
        internal NeoSystem? _system;
        private static readonly byte[] OnPersistScript = BuildScript(ApplicationEngine.System_Contract_NativeOnPersist);
        private static readonly byte[] PostPersistScript = BuildScript(ApplicationEngine.System_Contract_NativePostPersist);
        private static readonly HttpClient HttpClient = new();
        private static readonly JsonSerializerOptions SupabaseJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public override string Description => "Replay a block against a provided key-value snapshot file for debugging.";
        public override string ConfigFile => Combine(RootPath, "StateReplay.json");

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            _system = system;
            ConsoleHelper.Info($"StateReplay: Loaded for network {system.Settings.Network:X8}");

            // Ensure cache directory exists
            if (!string.IsNullOrEmpty(Settings.Default.CacheDirectory))
            {
                Directory.CreateDirectory(Settings.Default.CacheDirectory);
            }
        }
        private static byte[] BuildScript(uint syscall)
        {
            using var sb = new ScriptBuilder();
            sb.EmitSysCall(syscall);
            return sb.ToArray();
        }
    }
}
