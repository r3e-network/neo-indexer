// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.Supabase.Replay.cs file belongs to the neo project and is free
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
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace StateReplay
{
    public partial class StateReplayPlugin
    {
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

            using var memoryStore = new MemoryStore();
            using var storeSnapshot = memoryStore.GetSnapshot();
            using var snapshotCache = new StoreCache(storeSnapshot);

            var loaded = 0;
            foreach (var row in rows)
            {
                if (row.ContractId is null)
                    continue;
                if (string.IsNullOrEmpty(row.KeyBase64) || row.ValueBase64 is null)
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
    }
}
