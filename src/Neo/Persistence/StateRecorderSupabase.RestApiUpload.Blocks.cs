// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RestApiUpload.Blocks.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
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
    }
}

