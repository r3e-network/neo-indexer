// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.JsonCsvUpload.PayloadBuilders.cs file belongs to the neo project and is free
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
using System.Globalization;
using System.Linq;
using System.Text;
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

        private static (string Content, string Path) BuildCsvPayload(BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("block_index,contract_id,contract_hash,manifest_name,key_base64,value_base64,read_order,tx_hash,source");
            foreach (var entry in entries)
            {
                var blockIndex = recorder.BlockIndex;
                var contractId = entry.Key.Id.ToString(CultureInfo.InvariantCulture);
                var contractHash = entry.ContractHash.ToString();
                var manifestName = entry.ManifestName ?? string.Empty;
                var keyBase64 = Convert.ToBase64String(entry.Key.ToArray());
                var valueBase64 = Convert.ToBase64String(entry.Value.Value.Span);
                var readOrder = entry.Order.ToString(CultureInfo.InvariantCulture);
                var txHash = entry.TxHash?.ToString() ?? string.Empty;
                var source = entry.Source ?? string.Empty;

                sb.Append(blockIndex).Append(',')
                    .Append(contractId).Append(',')
                    .Append(EscapeCsv(contractHash)).Append(',')
                    .Append(EscapeCsv(manifestName)).Append(',')
                    .Append(EscapeCsv(keyBase64)).Append(',')
                    .Append(EscapeCsv(valueBase64)).Append(',')
                    .Append(readOrder).Append(',')
                    .Append(EscapeCsv(txHash)).Append(',')
                    .Append(EscapeCsv(source))
                    .AppendLine();
            }

            return (sb.ToString(), $"block-{recorder.BlockIndex}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
            var escaped = value.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{escaped}\"" : escaped;
        }
    }
}

