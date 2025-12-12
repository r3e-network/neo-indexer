// Copyright (C) 2015-2025 The Neo Project.
//
// UT_Settings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text.Json;

namespace StateReplay.Tests
{
    [TestClass]
    public class UT_Settings
    {
        [TestMethod]
        public void Default_HasCorrectInitialValues()
        {
            // Settings.Default is a singleton, reset by loading empty config
            var emptyConfig = new ConfigurationBuilder().Build().GetSection("PluginConfiguration");
            Settings.Load(emptyConfig);

            Assert.IsTrue(Settings.Default.Enabled);
            Assert.AreEqual(string.Empty, Settings.Default.SupabaseUrl);
            Assert.AreEqual(string.Empty, Settings.Default.SupabaseApiKey);
            Assert.AreEqual("block-state", Settings.Default.SupabaseBucket);
            Assert.AreEqual("state-replay-cache", Settings.Default.CacheDirectory);
            Assert.IsFalse(Settings.Default.ComparisonModeEnabled);
        }

        [TestMethod]
        public void Load_WithCustomValues_SetsAllProperties()
        {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, JsonSerializer.Serialize(new
            {
                PluginConfiguration = new
                {
                    Enabled = false,
                    SupabaseUrl = "https://test.supabase.co",
                    SupabaseApiKey = "test-api-key",
                    SupabaseBucket = "custom-bucket",
                    CacheDirectory = "custom-cache",
                    ComparisonModeEnabled = true
                }
            }));

            var config = new ConfigurationBuilder()
                .AddJsonFile(tempFile)
                .Build();

            Settings.Load(config.GetSection("PluginConfiguration"));

            Assert.IsFalse(Settings.Default.Enabled);
            Assert.AreEqual("https://test.supabase.co", Settings.Default.SupabaseUrl);
            Assert.AreEqual("test-api-key", Settings.Default.SupabaseApiKey);
            Assert.AreEqual("custom-bucket", Settings.Default.SupabaseBucket);
            Assert.AreEqual("custom-cache", Settings.Default.CacheDirectory);
            Assert.IsTrue(Settings.Default.ComparisonModeEnabled);

            File.Delete(tempFile);
        }

        [TestMethod]
        public void Load_WithPartialValues_UsesDefaults()
        {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, JsonSerializer.Serialize(new
            {
                PluginConfiguration = new
                {
                    SupabaseUrl = "https://partial.supabase.co"
                }
            }));

            var config = new ConfigurationBuilder()
                .AddJsonFile(tempFile)
                .Build();

            Settings.Load(config.GetSection("PluginConfiguration"));

            // Custom value
            Assert.AreEqual("https://partial.supabase.co", Settings.Default.SupabaseUrl);

            // Default values
            Assert.IsTrue(Settings.Default.Enabled);
            Assert.AreEqual(string.Empty, Settings.Default.SupabaseApiKey);
            Assert.AreEqual("block-state", Settings.Default.SupabaseBucket);
            Assert.AreEqual("state-replay-cache", Settings.Default.CacheDirectory);
            Assert.IsFalse(Settings.Default.ComparisonModeEnabled);

            File.Delete(tempFile);
        }

        [TestMethod]
        public void Default_IsSingleton()
        {
            var settings1 = Settings.Default;
            var settings2 = Settings.Default;

            Assert.AreSame(settings1, settings2);
        }

        [TestMethod]
        public void Load_UpdatesSingleton()
        {
            var before = Settings.Default;

            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, JsonSerializer.Serialize(new
            {
                PluginConfiguration = new
                {
                    SupabaseUrl = "https://updated.supabase.co"
                }
            }));

            var config = new ConfigurationBuilder()
                .AddJsonFile(tempFile)
                .Build();

            Settings.Load(config.GetSection("PluginConfiguration"));

            var after = Settings.Default;

            // New instance after Load
            Assert.AreNotSame(before, after);
            Assert.AreEqual("https://updated.supabase.co", after.SupabaseUrl);

            File.Delete(tempFile);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Reset settings to defaults
            var emptyConfig = new ConfigurationBuilder().Build().GetSection("PluginConfiguration");
            Settings.Load(emptyConfig);
        }
    }
}
