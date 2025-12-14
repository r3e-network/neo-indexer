// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.Commands.Binary.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.IO;

namespace StateReplay
{
    public partial class StateReplayPlugin
    {
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

            using var memoryStore = new MemoryStore();
            using var storeSnapshot = memoryStore.GetSnapshot();
            using var snapshotCache = new StoreCache(storeSnapshot);

            foreach (var entry in binaryFile.Entries)
            {
                var storageKey = (StorageKey)entry.Key;
                var storageItem = new StorageItem(entry.Value);
                snapshotCache.Add(storageKey, storageItem);
            }

            ReplayBlock(block, snapshotCache);
            ConsoleHelper.Info("Replay", $"Loaded {binaryFile.Entries.Count} entries from binary snapshot (block {blockIndex}).");
        }
    }
}

