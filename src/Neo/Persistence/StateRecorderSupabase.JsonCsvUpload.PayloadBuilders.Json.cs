// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.JsonCsvUpload.PayloadBuilders.Json.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static (string Content, string Path) BuildJsonPayload(BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
            var keys = new List<object>(entries.Length);
            foreach (var entry in entries)
            {
                var keyBytes = entry.Key.ToArray();
                keys.Add(new
                {
                    key = Convert.ToBase64String(keyBytes),
                    value = Convert.ToBase64String(entry.Value.Value.Span),
                    readOrder = entry.Order,
                    contractId = entry.Key.Id,
                    contractHash = entry.ContractHash.ToString(),
                    manifestName = entry.ManifestName,
                    txHash = entry.TxHash?.ToString(),
                    source = entry.Source
                });
            }

            var payload = new
            {
                block = recorder.BlockIndex,
                hash = recorder.BlockHash.ToString(),
                timestamp = recorder.Timestamp,
                keyCount = entries.Length,
                txCount = entries.Select(e => e.TxHash).Where(h => h != null).Distinct().Count(),
                keys
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
            return (json, $"block-{recorder.BlockIndex}.json");
        }
    }
}

