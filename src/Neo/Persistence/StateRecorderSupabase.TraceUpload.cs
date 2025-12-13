// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.cs file belongs to the neo project and is free
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

	            await TraceUploadLaneSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
	            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
	            try
	            {
	                var blockIndexValue = checked((int)blockIndex);
	                var batchSize = GetTraceUploadBatchSize();

	                var contractHashCache = new Dictionary<UInt160, string>();
	                var opCodeRows = BuildOpCodeTraceRows(blockIndexValue, txHash, recorder.GetOpCodeTraces(), contractHashCache);
	                var syscallRows = BuildSyscallTraceRows(blockIndexValue, txHash, recorder.GetSyscallTraces(), contractHashCache);
	                var contractCallRows = BuildContractCallTraceRows(blockIndexValue, txHash, recorder.GetContractCallTraces(), contractHashCache);
	                var storageWriteRows = BuildStorageWriteTraceRows(blockIndexValue, txHash, recorder.GetStorageWriteTraces(), contractHashCache);
	                var notificationRows = BuildNotificationTraceRows(blockIndexValue, txHash, recorder.GetNotificationTraces(), contractHashCache);

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
	            finally
	            {
	                TraceUploadSemaphore.Release();
	                TraceUploadLaneSemaphore.Release();
	            }
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

            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
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

	                var payload = JsonSerializer.SerializeToUtf8Bytes(new[] { row });
	                // Explicit on_conflict for robustness.
	                await SendTraceRequestWithRetryAsync($"{baseUrl}/rest/v1/block_stats?on_conflict=block_index", apiKey, payload, "block stats").ConfigureAwait(false);

                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                    $"Block stats upsert successful for block {stats.BlockIndex}");
            }
            finally
            {
                TraceUploadSemaphore.Release();
            }
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

        private static string? GetTraceUpsertConflictTarget(string tableName)
        {
            return tableName switch
            {
                "opcode_traces" => "block_index,tx_hash,trace_order",
                "syscall_traces" => "block_index,tx_hash,trace_order",
                "contract_calls" => "block_index,tx_hash,trace_order",
                "storage_writes" => "block_index,tx_hash,write_order",
                "notifications" => "block_index,tx_hash,notification_order",
                _ => null
            };
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
            var conflictTarget = GetTraceUpsertConflictTarget(tableName);
            var requestUri = conflictTarget is null
                ? $"{baseUrl}/rest/v1/{tableName}"
                : $"{baseUrl}/rest/v1/{tableName}?on_conflict={conflictTarget}";

	            for (var offset = 0; offset < rows.Count; offset += effectiveBatchSize)
	            {
	                var count = Math.Min(effectiveBatchSize, rows.Count - offset);
	                var batch = rows.Skip(offset).Take(count);
	                var payload = JsonSerializer.SerializeToUtf8Bytes(batch);
	                await SendTraceRequestWithRetryAsync(
	                    requestUri,
	                    apiKey,
	                    payload,
	                    entityName).ConfigureAwait(false);
            }
        }

	        private static async Task SendTraceRequestWithRetryAsync(string requestUri, string apiKey, byte[] jsonPayload, string entityName)
	        {
	            var delay = TimeSpan.FromSeconds(1);
	            const int maxAttempts = 5;

	            for (var attempt = 1; attempt <= maxAttempts; attempt++)
	            {
	                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
	                {
	                    Content = new ByteArrayContent(jsonPayload)
	                };
	                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
	                AddRestApiHeaders(request, apiKey);
	                request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");

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

	        private static List<OpCodeTraceRow> BuildOpCodeTraceRows(
	            int blockIndex,
	            string txHash,
	            IReadOnlyList<OpCodeTrace> traces,
	            Dictionary<UInt160, string> contractHashCache)
	        {
	            var rows = new List<OpCodeTraceRow>(traces.Count);
	            foreach (var trace in traces)
	            {
	                var operand = trace.Operand.IsEmpty ? null : Convert.ToBase64String(trace.Operand.Span);
	                var contractHash = trace.ContractHash ?? UInt160.Zero;
	                var contractHashString = GetContractHashString(contractHash, contractHashCache);
	                var opCodeName = GetOpCodeName(trace.OpCode);
	                rows.Add(new OpCodeTraceRow(
	                    blockIndex,
	                    txHash,
	                    trace.Order,
	                    contractHashString,
	                    trace.InstructionPointer,
	                    (int)trace.OpCode,
	                    opCodeName,
	                    operand,
	                    trace.GasConsumed,
	                    trace.StackDepth));
	            }
	            return rows;
	        }

	        private static List<SyscallTraceRow> BuildSyscallTraceRows(
	            int blockIndex,
	            string txHash,
	            IReadOnlyList<SyscallTrace> traces,
	            Dictionary<UInt160, string> contractHashCache)
	        {
	            var rows = new List<SyscallTraceRow>(traces.Count);
	            foreach (var trace in traces)
	            {
	                var contractHash = trace.ContractHash ?? UInt160.Zero;
	                var contractHashString = GetContractHashString(contractHash, contractHashCache);
	                rows.Add(new SyscallTraceRow(
	                    blockIndex,
	                    txHash,
	                    trace.Order,
	                    contractHashString,
	                    trace.SyscallHash,
	                    trace.SyscallName,
	                    trace.GasCost));
	            }
	            return rows;
	        }

	        private static List<ContractCallTraceRow> BuildContractCallTraceRows(
	            int blockIndex,
	            string txHash,
	            IReadOnlyList<ContractCallTrace> traces,
	            Dictionary<UInt160, string> contractHashCache)
	        {
	            var rows = new List<ContractCallTraceRow>(traces.Count);
	            foreach (var trace in traces)
	            {
	                var calleeHash = trace.CalleeHash ?? UInt160.Zero;
	                var calleeHashString = GetContractHashString(calleeHash, contractHashCache);
	                rows.Add(new ContractCallTraceRow(
	                    blockIndex,
	                    txHash,
	                    trace.Order,
	                    GetContractHashStringOrNull(trace.CallerHash, contractHashCache),
	                    calleeHashString,
	                    trace.MethodName,
	                    trace.CallDepth,
	                    trace.Success,
	                    trace.GasConsumed));
	            }
	            return rows;
	        }

	        private static List<StorageWriteTraceRow> BuildStorageWriteTraceRows(
	            int blockIndex,
	            string txHash,
	            IReadOnlyList<StorageWriteTrace> traces,
	            Dictionary<UInt160, string> contractHashCache)
	        {
	            var rows = new List<StorageWriteTraceRow>(traces.Count);
	            foreach (var trace in traces)
	            {
	                var contractHash = trace.ContractHash ?? UInt160.Zero;
	                var contractHashString = GetContractHashString(contractHash, contractHashCache);
	                rows.Add(new StorageWriteTraceRow(
	                    blockIndex,
	                    txHash,
	                    trace.Order,
	                    trace.ContractId,
	                    contractHashString,
	                    Convert.ToBase64String(trace.Key.Span),
	                    trace.OldValue.HasValue ? Convert.ToBase64String(trace.OldValue.Value.Span) : null,
	                    Convert.ToBase64String(trace.NewValue.Span)));
	            }
	            return rows;
	        }

	        private static List<NotificationTraceRow> BuildNotificationTraceRows(
	            int blockIndex,
	            string txHash,
	            IReadOnlyList<NotificationTrace> traces,
	            Dictionary<UInt160, string> contractHashCache)
	        {
	            var rows = new List<NotificationTraceRow>(traces.Count);
	            foreach (var trace in traces)
	            {
	                var contractHash = trace.ContractHash ?? UInt160.Zero;
	                var contractHashString = GetContractHashString(contractHash, contractHashCache);
	                rows.Add(new NotificationTraceRow(
	                    blockIndex,
	                    txHash,
	                    trace.Order,
	                    contractHashString,
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

	        private static int GetUploadQueueWorkers()
	        {
	            var raw = Environment.GetEnvironmentVariable(UploadQueueWorkersEnvVar);
	            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
	                return parsed;
	            return TraceUploadConcurrency;
	        }

        private static int GetPositiveEnvIntOrDefault(string envVar, int defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
                return parsed;
            return defaultValue;
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

	        private static int GetTraceUploadConcurrency()
	        {
	            var raw = Environment.GetEnvironmentVariable(TraceUploadConcurrencyEnvVar);
	            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
	                return parsed;
	            return 4;
	        }

	        private static int GetLowPriorityTraceLaneConcurrency()
	        {
	            // Reserve at least one global upload slot for high-priority uploads.
	            // When concurrency is 1, there is nothing to reserve; traces must use the only slot.
	            return TraceUploadConcurrency <= 1 ? 1 : TraceUploadConcurrency - 1;
	        }

	        #endregion
	}
}
