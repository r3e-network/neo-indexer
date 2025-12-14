// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.Compare.Snapshot.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StateReplay
{
    public partial class StateReplayPlugin
    {
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

            using var stream = File.OpenRead(filePath);
            using var json = JsonDocument.Parse(stream);
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
