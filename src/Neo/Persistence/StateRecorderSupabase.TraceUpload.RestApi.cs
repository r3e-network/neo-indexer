// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.RestApi.cs file belongs to the neo project and is free
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
			#region Trace REST API
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
		#endregion

	}
}
