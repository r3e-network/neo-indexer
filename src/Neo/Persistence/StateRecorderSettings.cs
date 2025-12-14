// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.SmartContract;
using System;

namespace Neo.Persistence
{
    public sealed partial class StateRecorderSettings
    {
        private const string Prefix = "NEO_STATE_RECORDER__";

        public enum UploadMode
        {
            Binary,
            Postgres, // Direct PostgreSQL upload using SupabaseConnectionString.
            Both,     // Binary + database (RestApi or Postgres).
            RestApi   // Uses Supabase PostgREST API (HTTPS)
        }

        public bool Enabled { get; init; }
        public string SupabaseUrl { get; init; } = string.Empty;
        public string SupabaseApiKey { get; init; } = string.Empty;
        public string SupabaseBucket { get; init; } = "block-state";
        public string SupabaseConnectionString { get; init; } = string.Empty;
        public UploadMode Mode { get; init; } = UploadMode.Binary;
        public ExecutionTraceLevel TraceLevel { get; init; } = ExecutionTraceLevel.All;
        /// <summary>
        /// When true, trace uploads will also delete any stale rows (e.g., when re-syncing or when trace level changes),
        /// ensuring trace tables exactly match the latest captured trace counts per transaction.
        /// </summary>
        public bool TrimStaleTraceRows { get; init; }
        /// <summary>
        /// When true, also upload per-block JSON/CSV exports to storage.
        /// Disabled by default to avoid creating large numbers of files.
        /// </summary>
        public bool UploadAuxFormats { get; init; }
        /// <summary>
        /// Optional cap on the number of unique storage keys recorded per block. 0 disables the cap.
        /// Useful to avoid unbounded memory growth and huge inserts when indexing public mainnet.
        /// </summary>
        public int MaxStorageReadsPerBlock { get; init; }
        public bool UploadEnabled => Enabled && !string.IsNullOrWhiteSpace(SupabaseUrl) && !string.IsNullOrWhiteSpace(SupabaseApiKey);

        public static StateRecorderSettings Current => Load();

        private static StateRecorderSettings Load()
        {
            var enabled = GetEnvBool("ENABLED");
            var supabaseUrl = Environment.GetEnvironmentVariable($"{Prefix}SUPABASE_URL") ?? string.Empty;
            var supabaseApiKey = Environment.GetEnvironmentVariable($"{Prefix}SUPABASE_KEY") ?? string.Empty;
            var modeValue = Environment.GetEnvironmentVariable($"{Prefix}UPLOAD_MODE");

            var mode = ParseUploadMode(modeValue);
            // Supabase-only deployments are the primary use case for this fork.
            // If the user configured Supabase URL/key but did not specify an upload mode,
            // default to RestApi so blocks/traces are persisted in Postgres.
            if (string.IsNullOrWhiteSpace(modeValue) &&
                enabled &&
                !string.IsNullOrWhiteSpace(supabaseUrl) &&
                !string.IsNullOrWhiteSpace(supabaseApiKey))
            {
                mode = UploadMode.RestApi;
            }

            return new StateRecorderSettings
            {
                Enabled = enabled,
                SupabaseUrl = supabaseUrl,
                SupabaseApiKey = supabaseApiKey,
                SupabaseBucket = Environment.GetEnvironmentVariable($"{Prefix}SUPABASE_BUCKET") ?? "block-state",
                SupabaseConnectionString = Environment.GetEnvironmentVariable($"{Prefix}SUPABASE_CONNECTION_STRING") ?? string.Empty,
                Mode = mode,
                TraceLevel = ParseTraceLevel(Environment.GetEnvironmentVariable($"{Prefix}TRACE_LEVEL")),
                TrimStaleTraceRows = GetEnvBool("TRACE_TRIM_STALE_ROWS"),
                UploadAuxFormats = GetEnvBool("UPLOAD_AUX_FORMATS"),
                MaxStorageReadsPerBlock = GetEnvInt("MAX_STORAGE_READS_PER_BLOCK")
            };
        }
    }
}
