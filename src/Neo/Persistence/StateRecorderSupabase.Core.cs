// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Core.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;

namespace Neo.Persistence
{
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

        // Global upload throttle to avoid Supabase overload on mainnet.
        // Despite the legacy name, this semaphore gates all uploads (snapshots, reads, traces, stats), including direct Postgres.
        private static readonly int TraceUploadConcurrency = GetTraceUploadConcurrency();
        private static readonly SemaphoreSlim TraceUploadSemaphore = new(TraceUploadConcurrency);

        // Prevent low-priority per-tx trace uploads from occupying all upload slots and starving high-priority uploads.
        private static readonly SemaphoreSlim TraceUploadLaneSemaphore = new(GetLowPriorityTraceLaneConcurrency());

        private static readonly UploadWorkQueue UploadQueue = new();
    }
}
