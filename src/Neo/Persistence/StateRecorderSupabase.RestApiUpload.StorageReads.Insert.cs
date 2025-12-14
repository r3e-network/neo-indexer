// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RestApiUpload.StorageReads.Insert.cs file belongs to the neo project and is free
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
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task InsertStorageReadsRestApiAsync(string baseUrl, string apiKey, List<StorageReadRecord> reads)
        {
            // Insert in batches to avoid request size limits
            for (var offset = 0; offset < reads.Count; offset += StorageReadBatchSize)
            {
                var batch = reads.Skip(offset).Take(StorageReadBatchSize).ToArray();
                var jsonArray = batch.Select(r => new
                {
                    block_index = r.BlockIndex,
                    contract_id = r.ContractId,
                    key_base64 = r.KeyBase64,
                    value_base64 = r.ValueBase64,
                    read_order = r.ReadOrder,
                    tx_hash = r.TxHash,
                    source = r.Source
                }).ToArray();

                var json = JsonSerializer.Serialize(jsonArray);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/rest/v1/storage_reads")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                AddRestApiHeaders(request, apiKey);
                request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");

                using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException($"REST API storage_reads insert failed: {(int)response.StatusCode} {body}");
                }
            }
        }
    }
}

