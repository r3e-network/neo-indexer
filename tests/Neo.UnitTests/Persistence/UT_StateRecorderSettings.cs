// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StateRecorderSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using System;
using System.Collections.Generic;

namespace Neo.UnitTests.Persistence
{
    [TestClass]
    public sealed class UT_StateRecorderSettings
    {
        private const string Prefix = "NEO_STATE_RECORDER__";
        private readonly Dictionary<string, string?> _envBackup = new(StringComparer.OrdinalIgnoreCase);

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var kv in _envBackup)
            {
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
            _envBackup.Clear();
        }

        private void SetEnv(string name, string? value)
        {
            var key = $"{Prefix}{name}";
            if (!_envBackup.ContainsKey(key))
            {
                _envBackup[key] = Environment.GetEnvironmentVariable(key);
            }
            Environment.SetEnvironmentVariable(key, value);
        }

        [TestMethod]
        public void Load_DefaultsToRestApi_WhenSupabaseConfiguredAndModeMissing()
        {
            SetEnv("ENABLED", "true");
            SetEnv("SUPABASE_URL", "https://example.supabase.co");
            SetEnv("SUPABASE_KEY", "service-role-key");
            SetEnv("UPLOAD_MODE", null);

            var settings = StateRecorderSettings.Current;
            Assert.IsTrue(settings.Enabled);
            Assert.AreEqual(StateRecorderSettings.UploadMode.RestApi, settings.Mode);
        }

        [TestMethod]
        public void Load_DefaultsToBinary_WhenSupabaseNotConfigured()
        {
            SetEnv("ENABLED", "true");
            SetEnv("SUPABASE_URL", string.Empty);
            SetEnv("SUPABASE_KEY", string.Empty);
            SetEnv("UPLOAD_MODE", null);

            var settings = StateRecorderSettings.Current;
            Assert.IsTrue(settings.Enabled);
            Assert.AreEqual(StateRecorderSettings.UploadMode.Binary, settings.Mode);
        }

        [TestMethod]
        public void Load_RespectsExplicitUploadMode()
        {
            SetEnv("ENABLED", "true");
            SetEnv("SUPABASE_URL", "https://example.supabase.co");
            SetEnv("SUPABASE_KEY", "service-role-key");
            SetEnv("UPLOAD_MODE", "Binary");

            var settings = StateRecorderSettings.Current;
            Assert.AreEqual(StateRecorderSettings.UploadMode.Binary, settings.Mode);
        }

        [TestMethod]
        public void Load_ParsesMaxStorageReadsPerBlock()
        {
            SetEnv("ENABLED", "true");
            SetEnv("SUPABASE_URL", "https://example.supabase.co");
            SetEnv("SUPABASE_KEY", "service-role-key");
            SetEnv("MAX_STORAGE_READS_PER_BLOCK", "123");

            var settings = StateRecorderSettings.Current;
            Assert.AreEqual(123, settings.MaxStorageReadsPerBlock);
        }

        [TestMethod]
        public void Load_ClampsInvalidMaxStorageReadsPerBlockToZero()
        {
            SetEnv("ENABLED", "true");
            SetEnv("SUPABASE_URL", "https://example.supabase.co");
            SetEnv("SUPABASE_KEY", "service-role-key");
            SetEnv("MAX_STORAGE_READS_PER_BLOCK", "-1");

            var settings = StateRecorderSettings.Current;
            Assert.AreEqual(0, settings.MaxStorageReadsPerBlock);
        }
    }
}
