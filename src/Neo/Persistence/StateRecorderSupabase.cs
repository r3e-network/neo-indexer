// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.cs file belongs to the neo project and is free
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
using NpgsqlTypes;
#endif

namespace Neo.Persistence
{
    /// <summary>
    /// Handles uploading recorded block state to Supabase.
    /// Supports binary uploads to Storage bucket and/or REST API inserts.
    /// Robust design: Supports re-sync by automatically replacing existing block data.
    /// </summary>
    public static class StateRecorderSupabase
    {
        private const int StorageReadBatchSize = 1000;
        private const ushort BinaryFormatVersion = 1;
        private static readonly byte[] BinaryMagic = [(byte)'N', (byte)'S', (byte)'B', (byte)'R'];

        private static readonly HttpClient HttpClient = new();
        private static readonly ConcurrentDictionary<int, ContractRecord> ContractCache = new();
        private const int DefaultTraceBatchSize = 1000;
        private const int MaxTraceBatchSize = 5000;
        private const string TraceBatchSizeEnvVar = "NEO_STATE_RECORDER__TRACE_BATCH_SIZE";

        /// <summary>
        /// Trigger upload of recorded block state based on configured mode.
        /// Runs asynchronously on background thread pool.
        /// </summary>
        public static void TryUpload(BlockReadRecorder recorder, StateRecorderSettings.UploadMode? modeOverride = null)
        {
            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled) return;

            var effectiveMode = modeOverride ?? settings.Mode;

            if (IsBinaryMode(effectiveMode) && settings.UploadEnabled)
            {
                _ = Task.Run(() => ExecuteWithRetryAsync(
                    () => UploadBinaryAsync(recorder, settings),
                    "binary upload",
                    recorder.BlockIndex));

                if (settings.UploadAuxFormats)
                {
                    _ = Task.Run(() => ExecuteWithRetryAsync(
                        () => UploadJsonAsync(recorder, settings),
                        "json upload",
                        recorder.BlockIndex));

                    _ = Task.Run(() => ExecuteWithRetryAsync(
                        () => UploadCsvAsync(recorder, settings),
                        "csv upload",
                        recorder.BlockIndex));
                }
            }

            // Database upload:
            // - RestApi/Both prefer Supabase PostgREST when configured, otherwise fall back to direct Postgres.
            // - Postgres mode always uses direct Postgres when a connection string is provided.
            if (effectiveMode == StateRecorderSettings.UploadMode.Postgres)
            {
                if (!string.IsNullOrWhiteSpace(settings.SupabaseConnectionString))
                {
                    _ = Task.Run(() => ExecuteWithRetryAsync(
                        () => UploadPostgresAsync(recorder, settings),
                        "PostgreSQL upsert",
                        recorder.BlockIndex));
                }
                else if (settings.UploadEnabled)
                {
                    _ = Task.Run(() => ExecuteWithRetryAsync(
                        () => UploadRestApiAsync(recorder, settings),
                        "REST API upsert",
                        recorder.BlockIndex));
                }
            }
            else if (IsRestApiMode(effectiveMode))
            {
                if (settings.UploadEnabled)
                {
                    _ = Task.Run(() => ExecuteWithRetryAsync(
                        () => UploadRestApiAsync(recorder, settings),
                        "REST API upsert",
                        recorder.BlockIndex));
                }
                else if (!string.IsNullOrWhiteSpace(settings.SupabaseConnectionString))
                {
                    _ = Task.Run(() => ExecuteWithRetryAsync(
                        () => UploadPostgresAsync(recorder, settings),
                        "PostgreSQL upsert",
                        recorder.BlockIndex));
                }
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility.
        /// </summary>
        internal static void TryUpload(BlockReadRecorder recorder, string format)
        {
            TryUpload(recorder);
        }

        private static bool IsBinaryMode(StateRecorderSettings.UploadMode mode)
            => mode is StateRecorderSettings.UploadMode.Binary or StateRecorderSettings.UploadMode.Both;

        private static bool IsRestApiMode(StateRecorderSettings.UploadMode mode)
            => mode is StateRecorderSettings.UploadMode.RestApi
                or StateRecorderSettings.UploadMode.Postgres
                or StateRecorderSettings.UploadMode.Both;

        private static async Task ExecuteWithRetryAsync(Func<Task> action, string description, uint blockIndex)
        {
            var delay = TimeSpan.FromSeconds(1);
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await action().ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                        $"Supabase {description} attempt {attempt}/3 failed for block {blockIndex}: {ex.Message}");
                    if (attempt == 3) return;
                    await Task.Delay(delay).ConfigureAwait(false);
                    delay += delay; // Exponential backoff: 1s, 2s, 4s
                }
            }
        }

        #region Binary Upload

        private static async Task UploadBinaryAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            var orderedEntries = GetOrderedEntries(recorder);
            var payload = BuildBinaryPayload(recorder, orderedEntries);

            // Use PUT for upsert behavior (overwrite if exists)
            using var request = new HttpRequestMessage(HttpMethod.Put,
                $"{settings.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{settings.SupabaseBucket}/{payload.Path}")
            {
                Content = new ByteArrayContent(payload.Buffer)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Headers.TryAddWithoutValidation("apikey", settings.SupabaseApiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SupabaseApiKey);
            request.Headers.TryAddWithoutValidation("x-upsert", "true");

            using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Supabase binary upload failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"Binary upload successful for block {recorder.BlockIndex}: {orderedEntries.Length} entries, {payload.Buffer.Length} bytes");
        }

        /// <summary>
        /// Build binary payload according to spec:
        /// Header: [Magic: 4 bytes "NSBR"] [Version: 2 bytes] [Block Index: 4 bytes] [Entry Count: 4 bytes]
        /// Entries: Array of [ContractHash: 20 bytes] [Key Length: 2 bytes] [Key: variable] [Value Length: 4 bytes] [Value: variable] [ReadOrder: 4 bytes]
        /// </summary>
        private static (byte[] Buffer, string Path) BuildBinaryPayload(BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            // Header
            writer.Write(BinaryMagic);
            writer.Write(BinaryFormatVersion);
            writer.Write(recorder.BlockIndex);
            writer.Write(entries.Length);

            // Entries
            foreach (var entry in entries)
            {
                // ContractHash: 20 bytes
                writer.Write(entry.ContractHash.ToArray());

                // Key
                var keyBytes = entry.Key.ToArray();
                if (keyBytes.Length > ushort.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"Key length {keyBytes.Length} exceeds max {ushort.MaxValue} for contract {entry.Key.Id}.");
                }
                writer.Write((ushort)keyBytes.Length);
                writer.Write(keyBytes);

                // Value
                var valueBytes = entry.Value.Value.ToArray();
                writer.Write(valueBytes.Length);
                writer.Write(valueBytes);

                // ReadOrder
                writer.Write(entry.Order);
            }

            writer.Flush();
            return (stream.ToArray(), $"block-{recorder.BlockIndex}.bin");
        }

        #endregion

        #region JSON/CSV Upload

        private static async Task UploadJsonAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            var entries = GetOrderedEntries(recorder);
            var jsonPayload = BuildJsonPayload(recorder, entries);

            using var request = new HttpRequestMessage(HttpMethod.Put,
                $"{settings.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{settings.SupabaseBucket}/{jsonPayload.Path}")
            {
                Content = new StringContent(jsonPayload.Content, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("apikey", settings.SupabaseApiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SupabaseApiKey);
            request.Headers.TryAddWithoutValidation("x-upsert", "true");

            using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Supabase json upload failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"JSON upload successful for block {recorder.BlockIndex}: {entries.Length} entries");
        }

        private static async Task UploadCsvAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            var entries = GetOrderedEntries(recorder);
            var csvPayload = BuildCsvPayload(recorder, entries);

            using var request = new HttpRequestMessage(HttpMethod.Put,
                $"{settings.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{settings.SupabaseBucket}/{csvPayload.Path}")
            {
                Content = new StringContent(csvPayload.Content, Encoding.UTF8, "text/csv")
            };
            request.Headers.TryAddWithoutValidation("apikey", settings.SupabaseApiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SupabaseApiKey);
            request.Headers.TryAddWithoutValidation("x-upsert", "true");

            using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Supabase csv upload failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"CSV upload successful for block {recorder.BlockIndex}: {entries.Length} entries");
        }

        private static (string Content, string Path) BuildJsonPayload(BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
            var keys = new List<object>(entries.Length);
            foreach (var entry in entries)
            {
                var keyBytes = entry.Key.ToArray();
                var valueBytes = entry.Value.Value.ToArray();
                keys.Add(new
                {
                    key = Convert.ToBase64String(keyBytes),
                    value = Convert.ToBase64String(valueBytes),
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
                var contractId = entry.Key.Id >= 0 ? entry.Key.Id.ToString() : string.Empty;
                var contractHash = entry.ContractHash.ToString();
                var manifestName = entry.ManifestName ?? string.Empty;
                var keyBase64 = Convert.ToBase64String(entry.Key.ToArray());
                var valueBase64 = Convert.ToBase64String(entry.Value.Value.ToArray());
                var readOrder = entry.Order.ToString();
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

        #endregion

        #region Record Builders

        private static BlockRecord BuildBlockRecord(BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
            var txHashes = new HashSet<UInt256>();
            foreach (var entry in entries)
            {
                if (entry.TxHash is { } txHash)
                {
                    txHashes.Add(txHash);
                }
            }

            var timestamp = recorder.Timestamp <= long.MaxValue ? (long)recorder.Timestamp : long.MaxValue;
            return new BlockRecord(
                checked((int)recorder.BlockIndex),
                recorder.BlockHash.ToString(),
                timestamp,
                txHashes.Count,
                entries.Length);
        }

        private static List<StorageReadRecord> BuildStorageReadRecords(BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
            var blockIndex = checked((int)recorder.BlockIndex);
            var reads = new List<StorageReadRecord>(entries.Length);
            foreach (var entry in entries)
            {
                var contractId = entry.Key.Id;
                reads.Add(new StorageReadRecord(
                    blockIndex,
                    contractId,
                    Convert.ToBase64String(entry.Key.Key.ToArray()),
                    Convert.ToBase64String(entry.Value.Value.ToArray()),
                    entry.Order,
                    entry.TxHash?.ToString(),
                    entry.Source));
            }
            return reads;
        }

        private static List<ContractRecord> BuildContractRecords(BlockReadEntry[] entries)
        {
            var records = new List<ContractRecord>();
            var seen = new HashSet<int>();
            foreach (var entry in entries)
            {
                var contractId = entry.Key.Id;
                if (!seen.Add(contractId)) continue; // Skip duplicates in this block
                if (ContractCache.ContainsKey(contractId)) continue; // Skip already cached

                records.Add(new ContractRecord(contractId, entry.ContractHash.ToString(), entry.ManifestName));
            }
            return records;
        }

        private static BlockReadEntry[] GetOrderedEntries(BlockReadRecorder recorder)
        {
            return recorder.Entries.OrderBy(entry => entry.Order).ToArray();
        }

        #endregion

        #region REST API Upload

        /// <summary>
        /// Upload block state using Supabase PostgREST API (HTTPS).
        /// This bypasses direct PostgreSQL connection, useful when IPv6 is blocked or pooler is unavailable.
        /// </summary>
        private static async Task UploadRestApiAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            var entries = GetOrderedEntries(recorder);
            var blockRecord = BuildBlockRecord(recorder, entries);
            var storageReads = BuildStorageReadRecords(recorder, entries);
            var contracts = BuildContractRecords(entries);

            var baseUrl = settings.SupabaseUrl.TrimEnd('/');
            var apiKey = settings.SupabaseApiKey;

            // Step 1: Delete existing storage_reads for this block (supports re-sync)
            await DeleteStorageReadsRestApiAsync(baseUrl, apiKey, blockRecord.BlockIndex).ConfigureAwait(false);

            // Step 2: Upsert block record
            await UpsertBlockRestApiAsync(baseUrl, apiKey, blockRecord).ConfigureAwait(false);

            // Step 3: Upsert contracts (if any new ones)
            if (contracts.Count > 0)
            {
                await UpsertContractsRestApiAsync(baseUrl, apiKey, contracts).ConfigureAwait(false);
            }

            // Step 4: Insert storage reads in batches
            if (storageReads.Count > 0)
            {
                await InsertStorageReadsRestApiAsync(baseUrl, apiKey, storageReads).ConfigureAwait(false);
            }

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"REST API upsert successful for block {recorder.BlockIndex}: {storageReads.Count} reads, {contracts.Count} new contracts");
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

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/rest/v1/blocks")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            AddRestApiHeaders(request, apiKey);
            request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");

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

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/rest/v1/contracts")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            AddRestApiHeaders(request, apiKey);
            request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");

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

                using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException($"REST API storage_reads insert failed: {(int)response.StatusCode} {body}");
                }
            }
        }

        private static void AddRestApiHeaders(HttpRequestMessage request, string apiKey)
        {
            request.Headers.TryAddWithoutValidation("apikey", apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        #endregion

        #region PostgreSQL Direct Upload

#if NET9_0_OR_GREATER
        private static async Task UploadPostgresAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            var entries = GetOrderedEntries(recorder);
            var blockRecord = BuildBlockRecord(recorder, entries);
            var storageReads = BuildStorageReadRecords(recorder, entries);
            var contracts = BuildContractRecords(entries);

            await using var connection = new NpgsqlConnection(settings.SupabaseConnectionString);
            await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

            await DeleteStorageReadsPostgresAsync(connection, transaction, blockRecord.BlockIndex).ConfigureAwait(false);
            await UpsertBlockPostgresAsync(connection, transaction, blockRecord).ConfigureAwait(false);

            if (contracts.Count > 0)
            {
                await UpsertContractsPostgresAsync(connection, transaction, contracts).ConfigureAwait(false);
            }

            if (storageReads.Count > 0)
            {
                await InsertStorageReadsPostgresAsync(connection, transaction, storageReads).ConfigureAwait(false);
            }

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"PostgreSQL upsert successful for block {recorder.BlockIndex}: {storageReads.Count} reads, {contracts.Count} new contracts");
        }

        private static async Task DeleteStorageReadsPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int blockIndex)
        {
            await using var command = new NpgsqlCommand(
                "DELETE FROM storage_reads WHERE block_index = @block_index",
                connection,
                transaction);
            command.Parameters.AddWithValue("block_index", blockIndex);
            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task UpsertBlockPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, BlockRecord block)
        {
            const string sql = @"
INSERT INTO blocks (block_index, hash, timestamp_ms, tx_count, read_key_count)
VALUES (@block_index, @hash, @timestamp_ms, @tx_count, @read_key_count)
ON CONFLICT (block_index) DO UPDATE SET
    hash = EXCLUDED.hash,
    timestamp_ms = EXCLUDED.timestamp_ms,
    tx_count = EXCLUDED.tx_count,
    read_key_count = EXCLUDED.read_key_count,
    updated_at = NOW();";

            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("block_index", block.BlockIndex);
            command.Parameters.AddWithValue("hash", block.Hash);
            command.Parameters.AddWithValue("timestamp_ms", block.TimestampMs);
            command.Parameters.AddWithValue("tx_count", block.TransactionCount);
            command.Parameters.AddWithValue("read_key_count", block.ReadKeyCount);
            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task UpsertContractsPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<ContractRecord> contracts)
        {
            var columns = new[] { "contract_id", "contract_hash", "manifest_name" };
            var values = contracts.Select(c => new object?[]
            {
                c.ContractId,
                c.ContractHash,
                c.ManifestName
            }).ToList();

            await UpsertRowsPostgresAsync(
                connection,
                transaction,
                "contracts",
                columns,
                "contract_id",
                "contract_hash = EXCLUDED.contract_hash, manifest_name = EXCLUDED.manifest_name, updated_at = NOW()",
                values,
                batchSize: 500).ConfigureAwait(false);

            foreach (var contract in contracts)
            {
                ContractCache.TryAdd(contract.ContractId, contract);
            }
        }

        private static async Task InsertStorageReadsPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<StorageReadRecord> reads)
        {
            var columns = new[]
            {
                "block_index",
                "contract_id",
                "key_base64",
                "value_base64",
                "read_order",
                "tx_hash",
                "source"
            };

            var values = reads.Select(r => new object?[]
            {
                r.BlockIndex,
                r.ContractId,
                r.KeyBase64,
                r.ValueBase64,
                r.ReadOrder,
                r.TxHash,
                r.Source
            }).ToList();

            await InsertRowsPostgresAsync(
                connection,
                transaction,
                "storage_reads",
                columns,
                conflictTarget: null,
                updateSet: null,
                values,
                StorageReadBatchSize).ConfigureAwait(false);
        }

        private static async Task UpsertRowsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string tableName,
            string[] columns,
            string conflictTarget,
            string updateSet,
            IReadOnlyList<object?[]> rows,
            int batchSize)
        {
            await InsertRowsPostgresAsync(
                connection,
                transaction,
                tableName,
                columns,
                conflictTarget,
                updateSet,
                rows,
                batchSize).ConfigureAwait(false);
        }

        private static async Task InsertRowsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string tableName,
            string[] columns,
            string? conflictTarget,
            string? updateSet,
            IReadOnlyList<object?[]> rows,
            int batchSize)
        {
            if (rows.Count == 0)
                return;

            var effectiveBatchSize = batchSize > 0 ? batchSize : rows.Count;

            for (var offset = 0; offset < rows.Count; offset += effectiveBatchSize)
            {
                var count = Math.Min(effectiveBatchSize, rows.Count - offset);
                var sb = new StringBuilder();
                sb.Append("INSERT INTO ").Append(tableName).Append(" (")
                  .Append(string.Join(", ", columns)).Append(") VALUES ");

                for (var i = 0; i < count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('(');
                    for (var c = 0; c < columns.Length; c++)
                    {
                        if (c > 0) sb.Append(", ");
                        sb.Append("@p").Append(i).Append('_').Append(c);
                    }
                    sb.Append(')');
                }

                if (!string.IsNullOrWhiteSpace(conflictTarget))
                {
                    sb.Append(" ON CONFLICT (").Append(conflictTarget).Append(')');
                    if (!string.IsNullOrWhiteSpace(updateSet))
                        sb.Append(" DO UPDATE SET ").Append(updateSet);
                    else
                        sb.Append(" DO NOTHING");
                }

                await using var command = new NpgsqlCommand(sb.ToString(), connection, transaction);

                for (var i = 0; i < count; i++)
                {
                    var row = rows[offset + i];
                    for (var c = 0; c < columns.Length; c++)
                    {
                        var parameter = command.Parameters.AddWithValue($"p{i}_{c}", row[c] ?? DBNull.Value);
                        if (columns[c] == "state_json" && row[c] is string)
                        {
                            parameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
                        }
                    }
                }

                await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static async Task UploadBlockTracePostgresAsync(
            int blockIndex,
            string txHash,
            List<OpCodeTraceRow> opCodeRows,
            List<SyscallTraceRow> syscallRows,
            List<ContractCallTraceRow> contractCallRows,
            List<StorageWriteTraceRow> storageWriteRows,
            List<NotificationTraceRow> notificationRows,
            int batchSize,
            StateRecorderSettings settings)
        {
            await using var connection = new NpgsqlConnection(settings.SupabaseConnectionString);
            await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

            if (opCodeRows.Count > 0)
                await UpsertOpCodeTracesPostgresAsync(connection, transaction, opCodeRows, batchSize).ConfigureAwait(false);

            if (syscallRows.Count > 0)
                await UpsertSyscallTracesPostgresAsync(connection, transaction, syscallRows, batchSize).ConfigureAwait(false);

            if (contractCallRows.Count > 0)
                await UpsertContractCallTracesPostgresAsync(connection, transaction, contractCallRows, batchSize).ConfigureAwait(false);

            if (storageWriteRows.Count > 0)
                await UpsertStorageWriteTracesPostgresAsync(connection, transaction, storageWriteRows, batchSize).ConfigureAwait(false);

            if (notificationRows.Count > 0)
                await UpsertNotificationTracesPostgresAsync(connection, transaction, notificationRows, batchSize).ConfigureAwait(false);

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"PostgreSQL trace upload successful for tx {txHash} @ block {blockIndex}: opcode={opCodeRows.Count}, syscall={syscallRows.Count}, calls={contractCallRows.Count}, writes={storageWriteRows.Count}, notifications={notificationRows.Count}");
        }

        private static Task UpsertOpCodeTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<OpCodeTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "trace_order",
                "contract_hash",
                "instruction_pointer",
                "opcode",
                "opcode_name",
                "operand_base64",
                "gas_consumed",
                "stack_depth"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.TraceOrder,
                r.ContractHash,
                r.InstructionPointer,
                r.OpCode,
                r.OpCodeName,
                r.OperandBase64,
                r.GasConsumed,
                r.StackDepth
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "opcode_traces",
                columns,
                "block_index, tx_hash, trace_order",
                "contract_hash = EXCLUDED.contract_hash, instruction_pointer = EXCLUDED.instruction_pointer, opcode = EXCLUDED.opcode, opcode_name = EXCLUDED.opcode_name, operand_base64 = EXCLUDED.operand_base64, gas_consumed = EXCLUDED.gas_consumed, stack_depth = EXCLUDED.stack_depth",
                values,
                batchSize);
        }

        private static Task UpsertSyscallTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<SyscallTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "trace_order",
                "contract_hash",
                "syscall_hash",
                "syscall_name",
                "gas_cost"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.TraceOrder,
                r.ContractHash,
                r.SyscallHash,
                r.SyscallName,
                r.GasCost
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "syscall_traces",
                columns,
                "block_index, tx_hash, trace_order",
                "contract_hash = EXCLUDED.contract_hash, syscall_hash = EXCLUDED.syscall_hash, syscall_name = EXCLUDED.syscall_name, gas_cost = EXCLUDED.gas_cost",
                values,
                batchSize);
        }

        private static Task UpsertContractCallTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<ContractCallTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "trace_order",
                "caller_hash",
                "callee_hash",
                "method_name",
                "call_depth",
                "success",
                "gas_consumed"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.TraceOrder,
                r.CallerHash,
                r.CalleeHash,
                r.MethodName,
                r.CallDepth,
                r.Success,
                r.GasConsumed
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "contract_calls",
                columns,
                "block_index, tx_hash, trace_order",
                "caller_hash = EXCLUDED.caller_hash, callee_hash = EXCLUDED.callee_hash, method_name = EXCLUDED.method_name, call_depth = EXCLUDED.call_depth, success = EXCLUDED.success, gas_consumed = EXCLUDED.gas_consumed",
                values,
                batchSize);
        }

        private static Task UpsertStorageWriteTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<StorageWriteTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "write_order",
                "contract_id",
                "contract_hash",
                "key_base64",
                "old_value_base64",
                "new_value_base64"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.WriteOrder,
                r.ContractId,
                r.ContractHash,
                r.KeyBase64,
                r.OldValueBase64,
                r.NewValueBase64
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "storage_writes",
                columns,
                "block_index, tx_hash, write_order",
                "contract_id = EXCLUDED.contract_id, contract_hash = EXCLUDED.contract_hash, key_base64 = EXCLUDED.key_base64, old_value_base64 = EXCLUDED.old_value_base64, new_value_base64 = EXCLUDED.new_value_base64",
                values,
                batchSize);
        }

        private static Task UpsertNotificationTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<NotificationTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "notification_order",
                "contract_hash",
                "event_name",
                "state_json"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.NotificationOrder,
                r.ContractHash,
                r.EventName,
                r.StateJson.HasValue ? r.StateJson.Value.GetRawText() : null
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "notifications",
                columns,
                "block_index, tx_hash, notification_order",
                "contract_hash = EXCLUDED.contract_hash, event_name = EXCLUDED.event_name, state_json = EXCLUDED.state_json",
                values,
                batchSize);
        }

        private static async Task UploadBlockStatsPostgresAsync(BlockStats stats, StateRecorderSettings settings)
        {
            var columns = new[]
            {
                "block_index",
                "tx_count",
                "total_gas_consumed",
                "opcode_count",
                "syscall_count",
                "contract_call_count",
                "storage_read_count",
                "storage_write_count",
                "notification_count"
            };

            var values = new List<object?[]>(1)
            {
                new object?[]
                {
                    checked((int)stats.BlockIndex),
                    stats.TransactionCount,
                    stats.TotalGasConsumed,
                    stats.OpCodeCount,
                    stats.SyscallCount,
                    stats.ContractCallCount,
                    stats.StorageReadCount,
                    stats.StorageWriteCount,
                    stats.NotificationCount
                }
            };

            await using var connection = new NpgsqlConnection(settings.SupabaseConnectionString);
            await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

            await UpsertRowsPostgresAsync(
                connection,
                transaction,
                "block_stats",
                columns,
                "block_index",
                "tx_count = EXCLUDED.tx_count, total_gas_consumed = EXCLUDED.total_gas_consumed, opcode_count = EXCLUDED.opcode_count, syscall_count = EXCLUDED.syscall_count, contract_call_count = EXCLUDED.contract_call_count, storage_read_count = EXCLUDED.storage_read_count, storage_write_count = EXCLUDED.storage_write_count, notification_count = EXCLUDED.notification_count, updated_at = NOW()",
                values,
                batchSize: 1).ConfigureAwait(false);

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"PostgreSQL block stats upsert successful for block {stats.BlockIndex}");
        }
#else
        private static Task UploadPostgresAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                "PostgreSQL direct upload requires net9.0 or greater.");
            return Task.CompletedTask;
        }
#endif

        #endregion

        #region Trace REST API Upload

        /// <summary>
        /// Uploads the execution traces captured for a transaction via the Supabase REST API.
        /// </summary>
        public static async Task UploadBlockTraceAsync(uint blockIndex, ExecutionTraceRecorder recorder)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));

            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled || !IsRestApiMode(settings.Mode))
                return;

            if (!recorder.HasTraces)
                return;

            var txHash = recorder.TxHash?.ToString();
            if (string.IsNullOrEmpty(txHash))
            {
                throw new InvalidOperationException("ExecutionTraceRecorder must include a transaction hash before uploading traces.");
            }

            var blockIndexValue = checked((int)blockIndex);
            var batchSize = GetTraceUploadBatchSize();

            var opCodeRows = BuildOpCodeTraceRows(blockIndexValue, txHash, recorder.GetOpCodeTraces());
            var syscallRows = BuildSyscallTraceRows(blockIndexValue, txHash, recorder.GetSyscallTraces());
            var contractCallRows = BuildContractCallTraceRows(blockIndexValue, txHash, recorder.GetContractCallTraces());
            var storageWriteRows = BuildStorageWriteTraceRows(blockIndexValue, txHash, recorder.GetStorageWriteTraces());
            var notificationRows = BuildNotificationTraceRows(blockIndexValue, txHash, recorder.GetNotificationTraces());

            var useDirectPostgres = settings.Mode == StateRecorderSettings.UploadMode.Postgres || !settings.UploadEnabled;
            if (useDirectPostgres)
            {
                if (string.IsNullOrWhiteSpace(settings.SupabaseConnectionString))
                    return;

#if NET9_0_OR_GREATER
                await UploadBlockTracePostgresAsync(
                    blockIndexValue,
                    txHash,
                    opCodeRows,
                    syscallRows,
                    contractCallRows,
                    storageWriteRows,
                    notificationRows,
                    batchSize,
                    settings).ConfigureAwait(false);
#endif
                return;
            }

            var baseUrl = settings.SupabaseUrl.TrimEnd('/');
            var apiKey = settings.SupabaseApiKey;
            var uploadTasks = new List<Task>(5);

            if (opCodeRows.Count > 0)
            {
                uploadTasks.Add(UploadOpCodeTracesRestApiAsync(baseUrl, apiKey, opCodeRows, batchSize));
            }

            if (syscallRows.Count > 0)
            {
                uploadTasks.Add(UploadSyscallTracesRestApiAsync(baseUrl, apiKey, syscallRows, batchSize));
            }

            if (contractCallRows.Count > 0)
            {
                uploadTasks.Add(UploadContractCallTracesRestApiAsync(baseUrl, apiKey, contractCallRows, batchSize));
            }

            if (storageWriteRows.Count > 0)
            {
                uploadTasks.Add(UploadStorageWriteTracesRestApiAsync(baseUrl, apiKey, storageWriteRows, batchSize));
            }

            if (notificationRows.Count > 0)
            {
                uploadTasks.Add(UploadNotificationTracesRestApiAsync(baseUrl, apiKey, notificationRows, batchSize));
            }

            if (uploadTasks.Count == 0)
                return;

            await Task.WhenAll(uploadTasks).ConfigureAwait(false);

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"Trace upload successful for tx {txHash} @ block {blockIndex}: opcode={opCodeRows.Count}, syscall={syscallRows.Count}, calls={contractCallRows.Count}, writes={storageWriteRows.Count}, notifications={notificationRows.Count}");
        }

        /// <summary>
        /// Upload aggregated block statistics via the Supabase REST API.
        /// </summary>
        public static async Task UploadBlockStatsAsync(BlockStats stats)
        {
            if (stats is null) throw new ArgumentNullException(nameof(stats));

            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled || !IsRestApiMode(settings.Mode))
                return;

            var useDirectPostgres = settings.Mode == StateRecorderSettings.UploadMode.Postgres || !settings.UploadEnabled;
            if (useDirectPostgres)
            {
                if (string.IsNullOrWhiteSpace(settings.SupabaseConnectionString))
                    return;

#if NET9_0_OR_GREATER
                await UploadBlockStatsPostgresAsync(stats, settings).ConfigureAwait(false);
#endif
                return;
            }

            var baseUrl = settings.SupabaseUrl.TrimEnd('/');
            var apiKey = settings.SupabaseApiKey;

            var row = new BlockStatsRow(
                checked((int)stats.BlockIndex),
                stats.TransactionCount,
                stats.TotalGasConsumed,
                stats.OpCodeCount,
                stats.SyscallCount,
                stats.ContractCallCount,
                stats.StorageReadCount,
                stats.StorageWriteCount,
                stats.NotificationCount);

            var payload = JsonSerializer.Serialize(new[] { row });
            await SendTraceRequestWithRetryAsync($"{baseUrl}/rest/v1/block_stats", apiKey, payload, "block stats").ConfigureAwait(false);

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                $"Block stats upsert successful for block {stats.BlockIndex}");
        }

        private static Task UploadOpCodeTracesRestApiAsync(string baseUrl, string apiKey, IReadOnlyList<OpCodeTraceRow> rows, int batchSize)
        {
            return UploadTraceBatchRestApiAsync(baseUrl, apiKey, "opcode_traces", "opcode traces", rows, batchSize);
        }

        private static Task UploadSyscallTracesRestApiAsync(string baseUrl, string apiKey, IReadOnlyList<SyscallTraceRow> rows, int batchSize)
        {
            return UploadTraceBatchRestApiAsync(baseUrl, apiKey, "syscall_traces", "syscall traces", rows, batchSize);
        }

        private static Task UploadContractCallTracesRestApiAsync(string baseUrl, string apiKey, IReadOnlyList<ContractCallTraceRow> rows, int batchSize)
        {
            return UploadTraceBatchRestApiAsync(baseUrl, apiKey, "contract_calls", "contract call traces", rows, batchSize);
        }

        private static Task UploadStorageWriteTracesRestApiAsync(string baseUrl, string apiKey, IReadOnlyList<StorageWriteTraceRow> rows, int batchSize)
        {
            return UploadTraceBatchRestApiAsync(baseUrl, apiKey, "storage_writes", "storage write traces", rows, batchSize);
        }

        private static Task UploadNotificationTracesRestApiAsync(string baseUrl, string apiKey, IReadOnlyList<NotificationTraceRow> rows, int batchSize)
        {
            return UploadTraceBatchRestApiAsync(baseUrl, apiKey, "notifications", "notification traces", rows, batchSize);
        }

        private static async Task UploadTraceBatchRestApiAsync<T>(
            string baseUrl,
            string apiKey,
            string tableName,
            string entityName,
            IReadOnlyList<T> rows,
            int batchSize)
        {
            if (rows.Count == 0)
                return;

            var effectiveBatchSize = batchSize > 0 ? Math.Min(batchSize, MaxTraceBatchSize) : DefaultTraceBatchSize;

            for (var offset = 0; offset < rows.Count; offset += effectiveBatchSize)
            {
                var count = Math.Min(effectiveBatchSize, rows.Count - offset);
                var buffer = new T[count];
                for (var i = 0; i < count; i++)
                {
                    buffer[i] = rows[offset + i];
                }

                var payload = JsonSerializer.Serialize(buffer);
                await SendTraceRequestWithRetryAsync(
                    $"{baseUrl}/rest/v1/{tableName}",
                    apiKey,
                    payload,
                    entityName).ConfigureAwait(false);
            }
        }

        private static async Task SendTraceRequestWithRetryAsync(string requestUri, string apiKey, string jsonPayload, string entityName)
        {
            var delay = TimeSpan.FromSeconds(1);
            const int maxAttempts = 5;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                AddRestApiHeaders(request, apiKey);
                request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates");

                using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return;

                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                        $"Supabase REST API throttled ({(int)response.StatusCode}) for {entityName} batch attempt {attempt}/{maxAttempts}. Retrying…");

                    if (attempt == maxAttempts)
                    {
                        var finalBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new InvalidOperationException($"REST API {entityName} upload failed after retries: {(int)response.StatusCode} {finalBody}");
                    }

                    await Task.Delay(delay).ConfigureAwait(false);
                    delay += delay;
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"REST API {entityName} upload failed: {(int)response.StatusCode} {body}");
            }
        }

        private static List<OpCodeTraceRow> BuildOpCodeTraceRows(int blockIndex, string txHash, IReadOnlyList<OpCodeTrace> traces)
        {
            var rows = new List<OpCodeTraceRow>(traces.Count);
            foreach (var trace in traces)
            {
                var operand = trace.Operand.IsEmpty ? null : Convert.ToBase64String(trace.Operand.Span);
                rows.Add(new OpCodeTraceRow(
                    blockIndex,
                    txHash,
                    trace.Order,
                    trace.ContractHash.ToString(),
                    trace.InstructionPointer,
                    (int)trace.OpCode,
                    trace.OpCodeName,
                    operand,
                    trace.GasConsumed,
                    trace.StackDepth));
            }
            return rows;
        }

        private static List<SyscallTraceRow> BuildSyscallTraceRows(int blockIndex, string txHash, IReadOnlyList<SyscallTrace> traces)
        {
            var rows = new List<SyscallTraceRow>(traces.Count);
            foreach (var trace in traces)
            {
                rows.Add(new SyscallTraceRow(
                    blockIndex,
                    txHash,
                    trace.Order,
                    trace.ContractHash.ToString(),
                    trace.SyscallHash,
                    trace.SyscallName,
                    trace.GasCost));
            }
            return rows;
        }

        private static List<ContractCallTraceRow> BuildContractCallTraceRows(int blockIndex, string txHash, IReadOnlyList<ContractCallTrace> traces)
        {
            var rows = new List<ContractCallTraceRow>(traces.Count);
            foreach (var trace in traces)
            {
                rows.Add(new ContractCallTraceRow(
                    blockIndex,
                    txHash,
                    trace.Order,
                    trace.CallerHash?.ToString(),
                    trace.CalleeHash.ToString(),
                    trace.MethodName,
                    trace.CallDepth,
                    trace.Success,
                    trace.GasConsumed));
            }
            return rows;
        }

        private static List<StorageWriteTraceRow> BuildStorageWriteTraceRows(int blockIndex, string txHash, IReadOnlyList<StorageWriteTrace> traces)
        {
            var rows = new List<StorageWriteTraceRow>(traces.Count);
            foreach (var trace in traces)
            {
                rows.Add(new StorageWriteTraceRow(
                    blockIndex,
                    txHash,
                    trace.Order,
                    trace.ContractId,
                    trace.ContractHash.ToString(),
                    Convert.ToBase64String(trace.Key.Span),
                    trace.OldValue.HasValue ? Convert.ToBase64String(trace.OldValue.Value.Span) : null,
                    Convert.ToBase64String(trace.NewValue.Span)));
            }
            return rows;
        }

        private static List<NotificationTraceRow> BuildNotificationTraceRows(int blockIndex, string txHash, IReadOnlyList<NotificationTrace> traces)
        {
            var rows = new List<NotificationTraceRow>(traces.Count);
            foreach (var trace in traces)
            {
                rows.Add(new NotificationTraceRow(
                    blockIndex,
                    txHash,
                    trace.Order,
                    trace.ContractHash.ToString(),
                    trace.EventName,
                    ParseNotificationState(trace.StateJson)));
            }
            return rows;
        }

        private static JsonElement? ParseNotificationState(string? stateJson)
        {
            if (string.IsNullOrWhiteSpace(stateJson))
                return null;

            try
            {
                using var document = JsonDocument.Parse(stateJson);
                return document.RootElement.Clone();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int GetTraceUploadBatchSize()
        {
            var raw = Environment.GetEnvironmentVariable(TraceBatchSizeEnvVar);
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
            {
                return Math.Min(parsed, MaxTraceBatchSize);
            }
            return DefaultTraceBatchSize;
        }

        #endregion
    }
}
