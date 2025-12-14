// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.Commands.Compare.cs file belongs to the neo project and is free
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
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace StateReplay
{
    public partial class StateReplayPlugin
    {
        [ConsoleCommand("replay compare", Category = "Replay", Description = "Compare replay execution with live execution and generate diff report")]
        internal void CompareBlockExecution(string filePath)
        {
            var report = BuildComparisonReport(filePath);
            ConsoleHelper.Info(report);
        }

        private string BuildComparisonReport(string filePath)
        {
            if (_system is null)
                throw new InvalidOperationException("NeoSystem is not ready.");

            var (blockIndex, snapshotEntries, expectedKeySet) = LoadSnapshotEntries(filePath);

            var block = NativeContract.Ledger.GetBlock(_system.StoreView, blockIndex);
            if (block is null)
            {
                throw new InvalidOperationException($"Block {blockIndex} not found on this node.");
            }

            // Build memory store from the snapshot entries, then replay the block while capturing read hits/misses
            // at the store layer to spot missing data and over-capture.
            using var memoryStore = new MemoryStore();
            using (var loadSnapshot = memoryStore.GetSnapshot())
            {
                foreach (var (key, value) in snapshotEntries)
                {
                    loadSnapshot.Put(key, value);
                }
                loadSnapshot.Commit();
            }

            using var execSnapshotInner = memoryStore.GetSnapshot();
            using var execSnapshot = new ReadCapturingStoreSnapshot(execSnapshotInner);
            using var snapshotCache = new StoreCache(execSnapshot);

            ReplayBlock(block, snapshotCache);

            var hitKeySet = execSnapshot.HitKeys is HashSet<string> hits
                ? hits
                : new HashSet<string>(execSnapshot.HitKeys, StringComparer.Ordinal);
            var missKeySet = execSnapshot.MissKeys is HashSet<string> misses
                ? misses
                : new HashSet<string>(execSnapshot.MissKeys, StringComparer.Ordinal);

            var notReadCount = 0;
            var notReadSample = new List<string>(capacity: 10);
            foreach (var expectedKey in expectedKeySet)
            {
                if (hitKeySet.Contains(expectedKey))
                    continue;
                notReadCount++;
                if (notReadSample.Count < 10)
                    notReadSample.Add(expectedKey);
            }

            var unexpectedHitCount = 0;
            var unexpectedHitSample = new List<string>(capacity: 10);
            foreach (var hitKey in hitKeySet)
            {
                if (expectedKeySet.Contains(hitKey))
                    continue;
                unexpectedHitCount++;
                if (unexpectedHitSample.Count < 10)
                    unexpectedHitSample.Add(hitKey);
            }

            var report = new StringBuilder();
            report.AppendLine($"=== Block {blockIndex} Comparison Report ===");
            report.AppendLine($"Block Hash: {block.Hash}");
            report.AppendLine($"Transactions: {block.Transactions.Length}");
            report.AppendLine($"Snapshot Keys (unique): {expectedKeySet.Count}");
            report.AppendLine($"Replay Read Hits: {hitKeySet.Count}");
            report.AppendLine($"Replay Read Misses: {missKeySet.Count}");
            report.AppendLine($"Snapshot Keys Not Read: {notReadCount}");
            report.AppendLine($"Unexpected Read Hits: {unexpectedHitCount}");

            if (missKeySet.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Read misses (base64, sample):");
                foreach (var key in missKeySet.Take(10))
                    report.AppendLine($"  {key}");
            }

            if (notReadSample.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Snapshot keys not read (base64, sample):");
                foreach (var key in notReadSample)
                    report.AppendLine($"  {key}");
            }

            if (unexpectedHitSample.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Unexpected read hits (base64, sample):");
                foreach (var key in unexpectedHitSample)
                    report.AppendLine($"  {key}");
            }

            report.AppendLine();
            report.AppendLine("Comparison complete.");

            return report.ToString();
        }

        private static (uint BlockIndex, IReadOnlyList<(byte[] Key, byte[] Value)> Entries, HashSet<string> ExpectedKeys) LoadSnapshotEntries(string filePath)
        {
            if (BinaryFormatReader.IsBinaryFormat(filePath))
            {
                var file = BinaryFormatReader.Read(filePath);
                var expectedKeys = new HashSet<string>(StringComparer.Ordinal);
                var entries = new List<(byte[] Key, byte[] Value)>(file.Entries.Count);
                foreach (var entry in file.Entries)
                {
                    expectedKeys.Add(Convert.ToBase64String(entry.Key));
                    entries.Add((entry.Key, entry.Value));
                }
                return (file.BlockIndex, entries, expectedKeys);
            }

            using var json = JsonDocument.Parse(File.ReadAllBytes(filePath));
            var root = json.RootElement;

            if (!root.TryGetProperty("block", out var blockElement) || blockElement.ValueKind != JsonValueKind.Number)
                throw new InvalidOperationException("Snapshot JSON missing numeric 'block' field.");
            if (!root.TryGetProperty("keys", out var keysElement) || keysElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Snapshot JSON missing 'keys' array.");

            var blockIndex = blockElement.GetUInt32();
            var expectedJsonKeys = new HashSet<string>(StringComparer.Ordinal);
            var jsonEntries = new List<(byte[] Key, byte[] Value)>();

            foreach (var entry in keysElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("key", out var keyProp) || !entry.TryGetProperty("value", out var valueProp))
                    throw new InvalidOperationException("Snapshot JSON entry missing 'key' or 'value'.");

                var keyBase64 = keyProp.GetString() ?? throw new InvalidOperationException("Snapshot JSON entry key is null.");
                var valueBase64 = valueProp.GetString() ?? throw new InvalidOperationException("Snapshot JSON entry value is null.");

                var keyBytes = Convert.FromBase64String(keyBase64);
                var valueBytes = Convert.FromBase64String(valueBase64);

                var canonicalKeyBase64 = Convert.ToBase64String(keyBytes);
                expectedJsonKeys.Add(canonicalKeyBase64);
                jsonEntries.Add((keyBytes, valueBytes));
            }

            return (blockIndex, jsonEntries, expectedJsonKeys);
        }
    }
}
