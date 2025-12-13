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
using System.Collections.Generic;
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

        // Global Supabase REST/Storage throttle to avoid 429 on mainnet.
        // Despite the legacy name, this semaphore gates all HTTPS uploads (snapshots, reads, traces, stats).
        private static readonly int TraceUploadConcurrency = GetTraceUploadConcurrency();
        private static readonly SemaphoreSlim TraceUploadSemaphore = new(TraceUploadConcurrency);

        // Prevent low-priority per-tx trace uploads from occupying all upload slots and starving high-priority uploads.
        private static readonly SemaphoreSlim TraceUploadLaneSemaphore = new(GetLowPriorityTraceLaneConcurrency());

        private static readonly UploadWorkQueue UploadQueue = new();

        private static string[] BuildOpCodeNameCache()
        {
            var names = new string[256];
            foreach (var opCode in (VM.OpCode[])Enum.GetValues(typeof(VM.OpCode)))
            {
                names[(byte)opCode] = opCode.ToString();
            }
            return names;
        }

        private static string GetOpCodeName(VM.OpCode opCode)
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

        private static bool IsBinaryMode(StateRecorderSettings.UploadMode mode)
            => mode is StateRecorderSettings.UploadMode.Binary or StateRecorderSettings.UploadMode.Both;

        private static bool IsRestApiMode(StateRecorderSettings.UploadMode mode)
            => mode is StateRecorderSettings.UploadMode.RestApi
                or StateRecorderSettings.UploadMode.Postgres
                or StateRecorderSettings.UploadMode.Both;
    }
}

