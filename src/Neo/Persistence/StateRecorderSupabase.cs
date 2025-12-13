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
    /// <summary>
    /// Handles uploading recorded block state to Supabase.
    /// Supports binary uploads to Storage bucket and/or REST API inserts.
    /// Robust design: Supports re-sync by automatically replacing existing block data.
    /// </summary>
	public static partial class StateRecorderSupabase
	{
	    private const int StorageReadBatchSize = 1000;
	    private const ushort BinaryFormatVersion = 1;
	    private static readonly byte[] BinaryMagic = [(byte)'N', (byte)'S', (byte)'B', (byte)'R'];
	    private static readonly string[] OpCodeNameCache = BuildOpCodeNameCache();

		    private static readonly HttpClient HttpClient = new();
		    private static readonly ConcurrentDictionary<int, ContractRecord> ContractCache = new();
		    private const int DefaultTraceBatchSize = 1000;
		    private const int MaxTraceBatchSize = 5000;
		    private const string TraceBatchSizeEnvVar = "NEO_STATE_RECORDER__TRACE_BATCH_SIZE";
		    private const string TraceUploadConcurrencyEnvVar = "NEO_STATE_RECORDER__TRACE_UPLOAD_CONCURRENCY";
		    private const string UploadQueueWorkersEnvVar = "NEO_STATE_RECORDER__UPLOAD_QUEUE_WORKERS";
		    private const string UploadQueueCapacityEnvVar = "NEO_STATE_RECORDER__UPLOAD_QUEUE_CAPACITY";
		    private const string TraceUploadQueueCapacityEnvVar = "NEO_STATE_RECORDER__TRACE_UPLOAD_QUEUE_CAPACITY";
		    // Global Supabase REST/Storage throttle to avoid 429 on mainnet.
		    // Despite the legacy name, this semaphore gates all HTTPS uploads (snapshots, reads, traces, stats).
		    private static readonly int TraceUploadConcurrency = GetTraceUploadConcurrency();
		    private static readonly SemaphoreSlim TraceUploadSemaphore = new(TraceUploadConcurrency);
		    // Prevent low-priority per-tx trace uploads from occupying all upload slots and starving high-priority uploads.
		    private static readonly SemaphoreSlim TraceUploadLaneSemaphore = new(GetLowPriorityTraceLaneConcurrency());
		    private static readonly UploadWorkQueue UploadQueue = new();

        public readonly record struct UploadQueueStats(
            int PendingHighPriority,
            int PendingLowPriority,
            long DroppedHighPriority,
            long DroppedLowPriority)
        {
            public int TotalPending => PendingHighPriority + PendingLowPriority;
        }

	    public static UploadQueueStats GetUploadQueueStats() => UploadQueue.GetStats();

	    private static string[] BuildOpCodeNameCache()
	    {
	        var names = new string[256];
	        foreach (var opCode in (Neo.VM.OpCode[])Enum.GetValues(typeof(Neo.VM.OpCode)))
	        {
	            names[(byte)opCode] = opCode.ToString();
	        }
	        return names;
	    }

	    private static string GetOpCodeName(Neo.VM.OpCode opCode)
	    {
	        return OpCodeNameCache[(byte)opCode] ?? opCode.ToString();
	    }

	    private static string GetContractHashString(UInt160 contractHash, Dictionary<UInt160, string> cache)
	    {
	        if (!cache.TryGetValue(contractHash, out var value))
	        {
	            value = contractHash.ToString();
	            cache[contractHash] = value;
	        }
	        return value;
	    }

	    private static string? GetContractHashStringOrNull(UInt160? contractHash, Dictionary<UInt160, string> cache)
	    {
	        if (contractHash is null) return null;
	        return GetContractHashString(contractHash, cache);
	    }

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

        private sealed class UploadWorkQueue
        {
            private const int DefaultHighPriorityCapacity = 2048;
            private const int DefaultLowPriorityCapacity = 16384;

            private readonly Channel<UploadWorkItem> _highPriority;
            private readonly Channel<UploadWorkItem> _lowPriority;
            private readonly int _highPriorityCapacity;
            private readonly int _lowPriorityCapacity;

            private int _pendingHighPriority;
            private int _pendingLowPriority;
            private long _droppedHighPriority;
            private long _droppedLowPriority;

	            public UploadWorkQueue()
	            {
	                _highPriorityCapacity = GetPositiveEnvIntOrDefault(UploadQueueCapacityEnvVar, DefaultHighPriorityCapacity);
	                _lowPriorityCapacity = GetPositiveEnvIntOrDefault(TraceUploadQueueCapacityEnvVar, DefaultLowPriorityCapacity);

                _highPriority = Channel.CreateBounded<UploadWorkItem>(new BoundedChannelOptions(_highPriorityCapacity)
                {
                    SingleReader = false,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                });

                _lowPriority = Channel.CreateBounded<UploadWorkItem>(new BoundedChannelOptions(_lowPriorityCapacity)
                {
                    SingleReader = false,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
	                });

	                var workers = GetUploadQueueWorkers();
	                if (workers <= 1)
	                {
	                    _ = Task.Run(() => WorkerLoopAsync(allowLowPriority: true));
	                }
	                else
	                {
	                    // Ensure high-priority uploads always have at least one dedicated worker,
	                    // even when low-priority trace uploads are slow.
	                    _ = Task.Run(() => WorkerLoopAsync(allowLowPriority: false));
	                    for (var i = 1; i < workers; i++)
	                    {
	                        _ = Task.Run(() => WorkerLoopAsync(allowLowPriority: true));
	                    }
	                }
	            }

            public UploadQueueStats GetStats()
            {
                return new UploadQueueStats(
                    PendingHighPriority: Volatile.Read(ref _pendingHighPriority),
                    PendingLowPriority: Volatile.Read(ref _pendingLowPriority),
                    DroppedHighPriority: Interlocked.Read(ref _droppedHighPriority),
                    DroppedLowPriority: Interlocked.Read(ref _droppedLowPriority));
            }

            public bool TryEnqueueHigh(uint blockIndex, string description, Func<Task> work)
            {
                return TryEnqueue(
                    _highPriority.Writer,
                    blockIndex,
                    description,
                    work,
                    isHighPriority: true);
            }

            public bool TryEnqueueLow(uint blockIndex, string description, Func<Task> work)
            {
                return TryEnqueue(
                    _lowPriority.Writer,
                    blockIndex,
                    description,
                    work,
                    isHighPriority: false);
            }

            private bool TryEnqueue(
                ChannelWriter<UploadWorkItem> writer,
                uint blockIndex,
                string description,
                Func<Task> work,
                bool isHighPriority)
            {
                if (work is null) throw new ArgumentNullException(nameof(work));

                if (isHighPriority)
                    Interlocked.Increment(ref _pendingHighPriority);
                else
                    Interlocked.Increment(ref _pendingLowPriority);

                var item = new UploadWorkItem(blockIndex, description, work, isHighPriority);
                if (writer.TryWrite(item))
                    return true;

                if (isHighPriority)
                    Interlocked.Decrement(ref _pendingHighPriority);
                else
                    Interlocked.Decrement(ref _pendingLowPriority);

                var dropped = isHighPriority
                    ? Interlocked.Increment(ref _droppedHighPriority)
                    : Interlocked.Increment(ref _droppedLowPriority);

                if (dropped == 1 || dropped % 100 == 0)
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                        $"Supabase upload queue full; dropped {(isHighPriority ? "high" : "low")} priority work. " +
                        $"pending_high={Volatile.Read(ref _pendingHighPriority)}/{_highPriorityCapacity}, " +
                        $"pending_low={Volatile.Read(ref _pendingLowPriority)}/{_lowPriorityCapacity}, " +
                        $"dropped_high={Interlocked.Read(ref _droppedHighPriority)}, dropped_low={Interlocked.Read(ref _droppedLowPriority)}. " +
                        $"Last dropped item: {description} (block {blockIndex}).");
                }

                return false;
            }

	            private async Task WorkerLoopAsync(bool allowLowPriority)
	            {
	                while (true)
	                {
	                    if (_highPriority.Reader.TryRead(out var item) || (allowLowPriority && _lowPriority.Reader.TryRead(out item)))
	                    {
	                        await ProcessAsync(item).ConfigureAwait(false);
	                        continue;
	                    }

	                    if (!allowLowPriority)
	                    {
	                        await _highPriority.Reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false);
	                        continue;
	                    }

	                    var highWait = _highPriority.Reader.WaitToReadAsync(CancellationToken.None).AsTask();
	                    var lowWait = _lowPriority.Reader.WaitToReadAsync(CancellationToken.None).AsTask();
	                    await Task.WhenAny(highWait, lowWait).ConfigureAwait(false);
	                }
	            }

            private async Task ProcessAsync(UploadWorkItem item)
            {
                try
                {
                    await item.Work().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                        $"Supabase queued {item.Description} failed for block {item.BlockIndex}: {ex.Message}");
                }
                finally
                {
                    if (item.IsHighPriority)
                        Interlocked.Decrement(ref _pendingHighPriority);
                    else
                        Interlocked.Decrement(ref _pendingLowPriority);
                }
            }

            private readonly record struct UploadWorkItem(
                uint BlockIndex,
                string Description,
                Func<Task> Work,
                bool IsHighPriority);
        }

	    }
	}
