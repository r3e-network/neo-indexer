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
				#region Trace Upload
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

	            var trimStaleTraceRows = settings.TrimStaleTraceRows;

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
	                        trimStaleTraceRows,
	                        settings).ConfigureAwait(false);
#endif
	                    return;
	                }

                var baseUrl = settings.SupabaseUrl.TrimEnd('/');
                var apiKey = settings.SupabaseApiKey;
                var uploadTasks = new List<Task>(5);

	                if (trimStaleTraceRows || opCodeRows.Count > 0)
	                {
	                    uploadTasks.Add(UploadAndMaybeTrimTraceTableRestApiAsync(
	                        baseUrl,
	                        apiKey,
	                        tableName: "opcode_traces",
	                        entityName: "opcode traces",
	                        opCodeRows,
	                        batchSize,
	                        blockIndexValue,
	                        txHash,
	                        trimStaleTraceRows));
	                }

	                if (trimStaleTraceRows || syscallRows.Count > 0)
	                {
	                    uploadTasks.Add(UploadAndMaybeTrimTraceTableRestApiAsync(
	                        baseUrl,
	                        apiKey,
	                        tableName: "syscall_traces",
	                        entityName: "syscall traces",
	                        syscallRows,
	                        batchSize,
	                        blockIndexValue,
	                        txHash,
	                        trimStaleTraceRows));
	                }

	                if (trimStaleTraceRows || contractCallRows.Count > 0)
	                {
	                    uploadTasks.Add(UploadAndMaybeTrimTraceTableRestApiAsync(
	                        baseUrl,
	                        apiKey,
	                        tableName: "contract_calls",
	                        entityName: "contract call traces",
	                        contractCallRows,
	                        batchSize,
	                        blockIndexValue,
	                        txHash,
	                        trimStaleTraceRows));
	                }

	                if (trimStaleTraceRows || storageWriteRows.Count > 0)
	                {
	                    uploadTasks.Add(UploadAndMaybeTrimTraceTableRestApiAsync(
	                        baseUrl,
	                        apiKey,
	                        tableName: "storage_writes",
	                        entityName: "storage write traces",
	                        storageWriteRows,
	                        batchSize,
	                        blockIndexValue,
	                        txHash,
	                        trimStaleTraceRows));
	                }

	                if (trimStaleTraceRows || notificationRows.Count > 0)
	                {
	                    uploadTasks.Add(UploadAndMaybeTrimTraceTableRestApiAsync(
	                        baseUrl,
	                        apiKey,
	                        tableName: "notifications",
	                        entityName: "notification traces",
	                        notificationRows,
	                        batchSize,
	                        blockIndexValue,
	                        txHash,
	                        trimStaleTraceRows));
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
			#endregion
	
		}
	}
