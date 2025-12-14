// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.Commands.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.ConsoleService;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.IO;
using System.Text.Json;

namespace StateReplay
{
    public partial class StateReplayPlugin
    {
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
    }
}
