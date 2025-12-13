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
    /// <summary>
    /// Handles uploading recorded block state to Supabase.
    /// Supports binary uploads to Storage bucket and/or REST API inserts.
    /// Robust design: Supports re-sync by automatically replacing existing block data.
    /// </summary>
	public static partial class StateRecorderSupabase
	{
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
                UploadQueue.TryEnqueueHigh(
                    recorder.BlockIndex,
                    "binary upload",
                    () => ExecuteWithRetryAsync(
                        () => UploadBinaryAsync(recorder, settings),
                        "binary upload",
                        recorder.BlockIndex));

                if (settings.UploadAuxFormats)
                {
                    UploadQueue.TryEnqueueHigh(
                        recorder.BlockIndex,
                        "json upload",
                        () => ExecuteWithRetryAsync(
                            () => UploadJsonAsync(recorder, settings),
                            "json upload",
                            recorder.BlockIndex));

                    UploadQueue.TryEnqueueHigh(
                        recorder.BlockIndex,
                        "csv upload",
                        () => ExecuteWithRetryAsync(
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
                    UploadQueue.TryEnqueueHigh(
                        recorder.BlockIndex,
                        "PostgreSQL upsert",
                        () => ExecuteWithRetryAsync(
                            () => UploadPostgresAsync(recorder, settings),
                            "PostgreSQL upsert",
                            recorder.BlockIndex));
                }
                else if (settings.UploadEnabled)
                {
                    UploadQueue.TryEnqueueHigh(
                        recorder.BlockIndex,
                        "REST API upsert",
                        () => ExecuteWithRetryAsync(
                            () => UploadRestApiAsync(recorder, settings),
                            "REST API upsert",
                            recorder.BlockIndex));
                }
            }
            else if (IsRestApiMode(effectiveMode))
            {
                if (settings.UploadEnabled)
                {
                    UploadQueue.TryEnqueueHigh(
                        recorder.BlockIndex,
                        "REST API upsert",
                        () => ExecuteWithRetryAsync(
                            () => UploadRestApiAsync(recorder, settings),
                            "REST API upsert",
                            recorder.BlockIndex));
                }
                else if (!string.IsNullOrWhiteSpace(settings.SupabaseConnectionString))
                {
                    UploadQueue.TryEnqueueHigh(
                        recorder.BlockIndex,
                        "PostgreSQL upsert",
                        () => ExecuteWithRetryAsync(
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

        /// <summary>
        /// Queue a transaction trace upload (low priority). Returns false if the queue is full and the work was dropped.
        /// </summary>
        public static bool TryQueueTraceUpload(uint blockIndex, ExecutionTraceRecorder recorder)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));
            if (!recorder.HasTraces) return true;

            var txHash = recorder.TxHash?.ToString() ?? "(unknown)";
            return UploadQueue.TryEnqueueLow(
                blockIndex,
                $"trace upload (tx={txHash})",
                () => ExecuteWithRetryAsync(
                    () => UploadBlockTraceAsync(blockIndex, recorder),
                    "trace upload",
                    blockIndex));
        }

        /// <summary>
        /// Queue a block stats upload (high priority). Returns false if the queue is full and the work was dropped.
        /// </summary>
        public static bool TryQueueBlockStatsUpload(BlockStats stats)
        {
            if (stats is null) throw new ArgumentNullException(nameof(stats));

            return UploadQueue.TryEnqueueHigh(
                stats.BlockIndex,
                "block stats upload",
                () => ExecuteWithRetryAsync(
                    () => UploadBlockStatsAsync(stats),
                    "block stats upload",
                    stats.BlockIndex));
        }
	    }
	}
