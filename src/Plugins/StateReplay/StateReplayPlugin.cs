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
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;
using System.Threading.Tasks;
using static System.IO.Path;

namespace StateReplay
{
    public class StateReplayPlugin : Plugin
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

        // Test hook to exercise replay without console command wiring.
        public void LoadForTest(NeoSystem system) => OnSystemLoaded(system);
        public void ReplayForTest(string filePath, uint? heightOverride = null) => ReplayBlockState(filePath, heightOverride);
        public void ReplayBinaryForTest(string filePath) => ReplayBlockStateBinary(filePath);

        #region Console Commands

        [ConsoleCommand("replay block-state", Category = "Replay", Description = "Replay a block using a key-value list file (JSON format)")]
        internal void ReplayBlockState(string filePath, uint? heightOverride = null)
        {
            if (_system is null)
                throw new InvalidOperationException("NeoSystem is not ready.");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Snapshot file not found", filePath);

            // Check if it's binary format
            if (BinaryFormatReader.IsBinaryFormat(filePath))
            {
                ReplayBlockStateBinary(filePath);
                return;
            }

            JsonDocument snapshotJson;
            try
            {
                snapshotJson = JsonDocument.Parse(File.ReadAllBytes(filePath));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Snapshot file is not valid JSON.", ex);
            }

            var root = snapshotJson.RootElement;
            if (!root.TryGetProperty("keys", out var keysElement) || keysElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Snapshot file missing 'keys' array.");
            if (!root.TryGetProperty("block", out var blockProperty))
                throw new InvalidOperationException("Snapshot file missing 'block' field.");

            var heightFromFile = blockProperty.GetUInt32();
            var height = heightOverride ?? heightFromFile;
            if (heightOverride.HasValue && heightOverride.Value != heightFromFile)
                throw new InvalidOperationException("Height override does not match snapshot block height.");

            var blockHash = root.TryGetProperty("hash", out var hashElem) ? UInt256.Parse(hashElem.GetString()) : throw new InvalidOperationException("Snapshot file missing 'hash' field.");
            if (blockHash is null)
                throw new InvalidOperationException($"Block {height} not found on this node.");

            var block = NativeContract.Ledger.GetBlock(_system.StoreView, blockHash);
            block ??= NativeContract.Ledger.GetBlock(_system.StoreView, height);
            if (block is null)
                throw new InvalidOperationException($"Block {height} not found on this node.");
            if (block.Index != height)
                throw new InvalidOperationException($"Snapshot height {height} does not match block index {block.Index} for hash {blockHash}.");

            var expectedCount = root.TryGetProperty("keyCount", out var kcElem) && kcElem.ValueKind == JsonValueKind.Number ? kcElem.GetInt32() : (int?)null;

            var memoryStore = new MemoryStore();
            using var storeSnapshot = memoryStore.GetSnapshot();
            using var snapshotCache = new StoreCache(storeSnapshot);

            var loaded = 0;
            foreach (var entry in keysElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("key", out var keyProp) || !entry.TryGetProperty("value", out var valueProp))
                    throw new InvalidOperationException("Snapshot entry missing 'key' or 'value'.");
                var keyBase64 = keyProp.GetString() ?? throw new InvalidOperationException("Snapshot entry key is null.");
                var valueBase64 = valueProp.GetString() ?? throw new InvalidOperationException("Snapshot entry value is null.");
                if (keyBase64.Length == 0) throw new InvalidOperationException("Snapshot entry key is empty.");
                if (valueBase64.Length == 0) throw new InvalidOperationException("Snapshot entry value is empty.");
                var keyBytes = Convert.FromBase64String(keyBase64);
                var valueBytes = Convert.FromBase64String(valueBase64);
                snapshotCache.Add((StorageKey)keyBytes, new StorageItem(valueBytes));
                loaded++;
            }

            if (expectedCount.HasValue && expectedCount.Value != loaded)
                throw new InvalidOperationException($"Snapshot keyCount mismatch: expected {expectedCount}, loaded {loaded}.");

            ReplayBlock(block, snapshotCache);
            ConsoleHelper.Info("Replay", $"Loaded {loaded} entries from JSON snapshot.");
        }

        [ConsoleCommand("replay block-binary", Category = "Replay", Description = "Replay a block using a binary NSBR format file")]
        internal void ReplayBlockStateBinary(string filePath)
        {
            if (_system is null)
                throw new InvalidOperationException("NeoSystem is not ready.");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Binary snapshot file not found", filePath);

            var binaryFile = BinaryFormatReader.Read(filePath);
            var blockIndex = binaryFile.BlockIndex;

            var block = NativeContract.Ledger.GetBlock(_system.StoreView, blockIndex);
            if (block is null)
                throw new InvalidOperationException($"Block {blockIndex} not found on this node.");

            var memoryStore = new MemoryStore();
            using var storeSnapshot = memoryStore.GetSnapshot();
            using var snapshotCache = new StoreCache(storeSnapshot);

            foreach (var entry in binaryFile.Entries)
            {
                // Cast byte array directly to StorageKey - it expects explicit conversion from byte[]
                var storageKey = (StorageKey)entry.Key;
                var storageItem = new StorageItem(entry.Value);
                snapshotCache.Add(storageKey, storageItem);
            }

            ReplayBlock(block, snapshotCache);
            ConsoleHelper.Info("Replay", $"Loaded {binaryFile.Entries.Count} entries from binary snapshot (block {blockIndex}).");
        }

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
            var localPath = Combine(Settings.Default.CacheDirectory, fileName);

            try
            {
                var task = DownloadFromSupabaseAsync(blockIndex, localPath);
                task.Wait();
                ConsoleHelper.Info("Replay", $"Downloaded block {blockIndex} to {localPath}");
            }
            catch (Exception ex)
            {
                ConsoleHelper.Error($"Download failed: {ex.Message}");
            }
        }

        [ConsoleCommand("replay compare", Category = "Replay", Description = "Compare replay execution with live execution and generate diff report")]
        internal void CompareBlockExecution(string filePath)
        {
            if (_system is null)
                throw new InvalidOperationException("NeoSystem is not ready.");

            // Load state file
            BinaryStateFile? binaryFile = null;
            Dictionary<string, byte[]>? jsonEntries = null;
            uint blockIndex;

            if (BinaryFormatReader.IsBinaryFormat(filePath))
            {
                binaryFile = BinaryFormatReader.Read(filePath);
                blockIndex = binaryFile.BlockIndex;
            }
            else
            {
                // JSON format
                var json = JsonDocument.Parse(File.ReadAllBytes(filePath));
                blockIndex = json.RootElement.GetProperty("block").GetUInt32();
                jsonEntries = new Dictionary<string, byte[]>();
                foreach (var entry in json.RootElement.GetProperty("keys").EnumerateArray())
                {
                    var key = entry.GetProperty("key").GetString()!;
                    var value = Convert.FromBase64String(entry.GetProperty("value").GetString()!);
                    jsonEntries[key] = value;
                }
            }

            var block = NativeContract.Ledger.GetBlock(_system.StoreView, blockIndex);
            if (block is null)
            {
                ConsoleHelper.Error($"Block {blockIndex} not found on this node.");
                return;
            }

            // Capture reads during replay
            var replayReads = new Dictionary<string, byte[]>();
            var liveReads = new Dictionary<string, byte[]>();

            // For comparison, we'd need to intercept reads during both executions
            // This is a simplified implementation that compares the loaded state
            var report = new StringBuilder();
            report.AppendLine($"=== Block {blockIndex} Comparison Report ===");
            report.AppendLine($"Block Hash: {block.Hash}");
            report.AppendLine($"Transactions: {block.Transactions.Length}");
            report.AppendLine();

            if (binaryFile != null)
            {
                report.AppendLine($"Snapshot Entries: {binaryFile.Entries.Count}");
                // Group by contract
                var byContract = binaryFile.Entries.GroupBy(e => e.ContractHash.ToString());
                foreach (var group in byContract.OrderBy(g => g.Key))
                {
                    report.AppendLine($"  Contract {group.Key}: {group.Count()} reads");
                }
            }
            else if (jsonEntries != null)
            {
                report.AppendLine($"Snapshot Entries: {jsonEntries.Count}");
            }

            report.AppendLine();
            report.AppendLine("Comparison complete. (Full diff requires live execution capture - not implemented in this version)");

            ConsoleHelper.Info(report.ToString());
        }

        #endregion

        #region Block Replay

        private void ReplayBlock(Block block, StoreCache snapshot)
        {
            var settings = _system!.Settings;
            var messages = new List<string>();

            using (ApplicationEngine engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, settings, 0))
            {
                engine.LoadScript(OnPersistScript);
                var state = engine.Execute();
                messages.Add($"OnPersist: {state}");
            }

            var clonedSnapshot = snapshot.CloneCache();
            foreach (Transaction tx in block.Transactions)
            {
                using ApplicationEngine engine = ApplicationEngine.Create(TriggerType.Application, tx, clonedSnapshot, block, settings, tx.SystemFee);
                engine.LoadScript(tx.Script);
                var state = engine.Execute();
                messages.Add($"Tx {tx.Hash}: {state}");
                if (state == VMState.HALT)
                    clonedSnapshot.Commit();
                else
                    clonedSnapshot = snapshot.CloneCache();
            }

            using (ApplicationEngine engine = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshot, block, settings, 0))
            {
                engine.LoadScript(PostPersistScript);
                var state = engine.Execute();
                messages.Add($"PostPersist: {state}");
            }

            ConsoleHelper.Info("Replay", $"Block {block.Index} ({block.Hash})");
            foreach (var line in messages)
            {
                ConsoleHelper.Info("Replay", line);
            }
        }

        #endregion

        #region Supabase Download

        private sealed class SupabaseStorageReadRow
        {
            [JsonPropertyName("contract_id")]
            public int? ContractId { get; set; }

            [JsonPropertyName("key_base64")]
            public string? KeyBase64 { get; set; }

            [JsonPropertyName("value_base64")]
            public string? ValueBase64 { get; set; }
        }

        private async Task ReplayFromSupabaseAsync(uint blockIndex)
        {
            if (_system is null)
                throw new InvalidOperationException("NeoSystem is not ready.");

            var block = NativeContract.Ledger.GetBlock(_system.StoreView, blockIndex);
            if (block is null)
                throw new InvalidOperationException($"Block {blockIndex} not found on this node.");

            var baseUrl = Settings.Default.SupabaseUrl.TrimEnd('/');
            var apiKey = Settings.Default.SupabaseApiKey;
            const int batchSize = 5000;

            var rows = new List<SupabaseStorageReadRow>();
            for (var offset = 0; ; offset += batchSize)
            {
                var url =
                    $"{baseUrl}/rest/v1/storage_reads" +
                    $"?select=contract_id,key_base64,value_base64" +
                    $"&block_index=eq.{blockIndex}" +
                    $"&order=read_order.asc" +
                    $"&limit={batchSize}" +
                    $"&offset={offset}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("apikey", apiKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Supabase query failed ({(int)response.StatusCode}): {body}");

                var batch = JsonSerializer.Deserialize<List<SupabaseStorageReadRow>>(body, SupabaseJsonOptions) ?? new List<SupabaseStorageReadRow>();
                if (batch.Count == 0)
                    break;

                rows.AddRange(batch);
                if (batch.Count < batchSize)
                    break;
            }

            var memoryStore = new MemoryStore();
            using var storeSnapshot = memoryStore.GetSnapshot();
            using var snapshotCache = new StoreCache(storeSnapshot);

            var loaded = 0;
            foreach (var row in rows)
            {
                if (row.ContractId is null)
                    continue;
                if (string.IsNullOrEmpty(row.KeyBase64) || string.IsNullOrEmpty(row.ValueBase64))
                    continue;

                var keyBytes = Convert.FromBase64String(row.KeyBase64);
                var valueBytes = Convert.FromBase64String(row.ValueBase64);

                var storageKey = new StorageKey { Id = row.ContractId.Value, Key = keyBytes };
                snapshotCache.Add(storageKey, new StorageItem(valueBytes));
                loaded++;
            }

            ReplayBlock(block, snapshotCache);
            ConsoleHelper.Info("Replay", $"Loaded {loaded} entries from Supabase storage_reads (block {blockIndex}).");
        }

        private async Task DownloadFromSupabaseAsync(uint blockIndex, string localPath)
        {
            var fileName = $"block-{blockIndex}.bin";
            var url = $"{Settings.Default.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{Settings.Default.SupabaseBucket}/{fileName}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("apikey", Settings.Default.SupabaseApiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.Default.SupabaseApiKey);

            using var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Download failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(localPath, bytes);
        }

        #endregion

        private static byte[] BuildScript(uint syscall)
        {
            using var sb = new ScriptBuilder();
            sb.EmitSysCall(syscall);
            return sb.ToArray();
        }
    }
}
