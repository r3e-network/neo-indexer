// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RestApiUpload.StorageReads.cs file belongs to the neo project and is free
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
        #region REST API Upload (Storage Reads)

        private static async Task DeleteStorageReadsRestApiAsync(string baseUrl, string apiKey, int blockIndex)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete,
                $"{baseUrl}/rest/v1/storage_reads?block_index=eq.{blockIndex}");
            AddRestApiHeaders(request, apiKey);

            using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            // 404 is OK (no records to delete), other errors should throw
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"REST API delete failed: {(int)response.StatusCode} {body}");
            }
        }

        private static async Task<bool> TryUpsertStorageReadsRestApiAsync(string baseUrl, string apiKey, List<StorageReadRecord> reads)
        {
            // Upsert in batches to avoid request size limits.
            // Requires a unique index covering (block_index, contract_id, key_base64) (migration 012).
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

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{baseUrl}/rest/v1/storage_reads?on_conflict=block_index,contract_id,key_base64")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                AddRestApiHeaders(request, apiKey);
                request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");

                using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    continue;

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (IsMissingUpsertConstraintError(body) || IsUpsertPermissionError(body))
                    return false;

                throw new InvalidOperationException($"REST API storage_reads upsert failed: {(int)response.StatusCode} {body}");
            }

            return true;
        }

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

        private static bool IsMissingUpsertConstraintError(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return false;

            // PostgREST typically returns: {"code":"42P10",...,"message":"there is no unique or exclusion constraint matching the ON CONFLICT specification"}
            return body.Contains("42P10", StringComparison.OrdinalIgnoreCase) ||
                   body.Contains("no unique", StringComparison.OrdinalIgnoreCase) ||
                   body.Contains("ON CONFLICT", StringComparison.OrdinalIgnoreCase) &&
                   body.Contains("constraint", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUpsertPermissionError(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return false;

            // Common PostgREST errors when UPDATE policies are missing:
            // {"code":"42501",...,"message":"new row violates row-level security policy for table \"storage_reads\""}
            return body.Contains("42501", StringComparison.OrdinalIgnoreCase) ||
                   body.Contains("row-level security", StringComparison.OrdinalIgnoreCase) ||
                   body.Contains("permission denied", StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}

