// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RestApiUpload.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Extensions;
using Neo.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
using NpgsqlTypes;
#endif


namespace Neo.Persistence
{
	public static partial class StateRecorderSupabase
	{
        #region REST API Upload

        /// <summary>
        /// Upload block state using Supabase PostgREST API (HTTPS).
        /// This bypasses direct PostgreSQL connection, useful when IPv6 is blocked or pooler is unavailable.
        /// </summary>
        private static async Task UploadRestApiAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
            var entries = GetOrderedEntries(recorder);
            var blockRecord = BuildBlockRecord(recorder, entries);
            var storageReads = BuildStorageReadRecords(recorder, entries);
            var contracts = BuildContractRecords(entries);

            var baseUrl = settings.SupabaseUrl.TrimEnd('/');
            var apiKey = settings.SupabaseApiKey;

            // Step 1: Upsert block record
            await UpsertBlockRestApiAsync(baseUrl, apiKey, blockRecord).ConfigureAwait(false);

            // Step 2: Upsert contracts (if any new ones)
            if (contracts.Count > 0)
            {
                await UpsertContractsRestApiAsync(baseUrl, apiKey, contracts).ConfigureAwait(false);
            }

            // Step 3: Upsert storage reads in batches (preferred, requires migration 012 unique index).
            // Falls back to delete+insert for older schemas.
            if (storageReads.Count > 0)
            {
                var upserted = await TryUpsertStorageReadsRestApiAsync(baseUrl, apiKey, storageReads).ConfigureAwait(false);
                if (!upserted)
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                        $"Block {recorder.BlockIndex}: storage_reads upsert not available (missing unique index). Falling back to delete+insert.");

                    await DeleteStorageReadsRestApiAsync(baseUrl, apiKey, blockRecord.BlockIndex).ConfigureAwait(false);
                    await InsertStorageReadsRestApiAsync(baseUrl, apiKey, storageReads).ConfigureAwait(false);
                }
            }

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"REST API upsert successful for block {recorder.BlockIndex}: {storageReads.Count} reads, {contracts.Count} new contracts");
            }
            finally
            {
                TraceUploadSemaphore.Release();
            }
        }

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
            var jsonArray = contracts.Select(c => new
            {
                contract_id = c.ContractId,
                contract_hash = c.ContractHash,
                manifest_name = c.ManifestName
            }).ToArray();

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

        private static void AddRestApiHeaders(HttpRequestMessage request, string apiKey)
        {
            request.Headers.TryAddWithoutValidation("apikey", apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        #endregion
	}
}
