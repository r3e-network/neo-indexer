// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RestApiUpload.BlocksContracts.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        #region REST API Upload (Blocks/Contracts)

        private static async Task UpsertBlockRestApiAsync(string baseUrl, string apiKey, BlockRecord block)
        {
            var json = JsonSerializer.Serialize(new
            {
                block_index = block.BlockIndex,
                hash = block.Hash,
                timestamp_ms = block.TimestampMs,
                tx_count = block.TransactionCount,
                read_key_count = block.ReadKeyCount
            });

            // Use on_conflict explicitly for robustness (legacy schemas may not have block_index as PK).
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/rest/v1/blocks?on_conflict=block_index")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            AddRestApiHeaders(request, apiKey);
            request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");

            using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"REST API block upsert failed: {(int)response.StatusCode} {body}");
            }
        }

        private static async Task UpsertContractsRestApiAsync(string baseUrl, string apiKey, List<ContractRecord> contracts)
        {
            var jsonArray = contracts.ConvertAll(c => new
            {
                contract_id = c.ContractId,
                contract_hash = c.ContractHash,
                manifest_name = c.ManifestName
            });

            var json = JsonSerializer.Serialize(jsonArray);

            // Use on_conflict explicitly for robustness (contracts are keyed by contract_id).
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/rest/v1/contracts?on_conflict=contract_id")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            AddRestApiHeaders(request, apiKey);
            request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");

            using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"REST API contracts upsert failed: {(int)response.StatusCode} {body}");
            }

            foreach (var contract in contracts)
            {
                ContractCache.TryAdd(contract.ContractId, contract);
            }
        }

        private static void AddRestApiHeaders(HttpRequestMessage request, string apiKey)
        {
            request.Headers.TryAddWithoutValidation("apikey", apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        #endregion
    }
}
