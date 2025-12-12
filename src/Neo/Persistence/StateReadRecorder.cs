// Copyright (C) 2015-2025 The Neo Project.
//
// StateReadRecorder.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Neo.Persistence
{
    /// <summary>
    /// Record of a single storage read during block execution.
    /// Captures the initial value at first read - subsequent reads of the same key are ignored.
    /// </summary>
    public sealed record BlockReadEntry(
        StorageKey Key,
        StorageItem Value,
        int Order,
        string? Source,
        UInt160 ContractHash,
        string? ManifestName,
        UInt256? TxHash);

    public sealed class BlockReadRecorder
    {
        private static readonly IReadOnlyDictionary<int, ContractMetadata> NativeContractsById =
            NativeContract.Contracts.ToDictionary(c => c.Id, c => new ContractMetadata(c.Hash, c.Name));

        private readonly List<BlockReadEntry> _entries = [];
        private readonly HashSet<StorageKey> _readKeys = [];
        private readonly object _entryLock = new();
        private readonly Dictionary<int, ContractMetadata> _contractMetadataCache = new();
        private readonly object _contractMetadataLock = new();
        private int _order;

        public uint BlockIndex { get; }
        public UInt256 BlockHash { get; }
        public ulong Timestamp { get; }
        public IReadOnlyCollection<BlockReadEntry> Entries => _entries;

        public BlockReadRecorder(Block block)
        {
            BlockIndex = block.Index;
            BlockHash = block.Hash;
            Timestamp = block.Timestamp;
        }

        /// <summary>
        /// Try to add a storage read entry. Only the FIRST read of each key is recorded.
        /// Thread-safe for concurrent transaction execution within a block.
        /// </summary>
        public bool TryAdd(IReadOnlyStore? store, StorageKey key, StorageItem value, string? source, UInt256? txHash)
        {
            // First-read deduplication: check if key already recorded
            lock (_entryLock)
            {
                if (!_readKeys.Add(key)) return false;
            }

            // Resolve contract metadata (outside lock to avoid blocking)
            var metadata = GetContractMetadata(store, key.Id);
            var entry = new BlockReadEntry(
                key,
                value.Clone(),
                Interlocked.Increment(ref _order),
                source,
                metadata.ContractHash,
                metadata.ManifestName,
                txHash);

            lock (_entryLock)
            {
                _entries.Add(entry);
            }

            return true;
        }

        private ContractMetadata GetContractMetadata(IReadOnlyStore? store, int contractId)
        {
            // Native contracts have negative IDs. Resolve from in-memory native contract list.
            if (contractId < 0)
            {
                if (NativeContractsById.TryGetValue(contractId, out var nativeMetadata))
                    return nativeMetadata;
                return ContractMetadata.Empty;
            }

            lock (_contractMetadataLock)
            {
                if (_contractMetadataCache.TryGetValue(contractId, out var cached))
                    return cached;
            }

            if (store is null) return ContractMetadata.Empty;

            ContractState? contractState = null;
            // Suppress recording to avoid recursive capture when querying contract metadata
            using (StateReadRecorder.SuppressRecordingScope())
            {
                contractState = NativeContract.ContractManagement.GetContractById(store, contractId);
            }

            var metadata = contractState is null
                ? ContractMetadata.Empty
                : new ContractMetadata(contractState.Hash, contractState.Manifest?.Name);

            lock (_contractMetadataLock)
            {
                _contractMetadataCache[contractId] = metadata;
            }

            return metadata;
        }

        private sealed record ContractMetadata(UInt160 ContractHash, string? ManifestName)
        {
            public static readonly ContractMetadata Empty = new(UInt160.Zero, null);
        }
    }

    public sealed class BlockReadRecorderScope : IDisposable
    {
        private readonly BlockReadRecorder? _previous;
        private readonly UInt256? _previousTransactionHash;
        public BlockReadRecorder Recorder { get; }

        public BlockReadRecorderScope(BlockReadRecorder recorder, BlockReadRecorder? previous)
        {
            Recorder = recorder;
            _previous = previous;
            _previousTransactionHash = StateReadRecorder.TransactionHash;
            StateReadRecorder.TransactionHash = null;
            StateReadRecorder.Current = recorder;
        }

        public void Dispose()
        {
            StateReadRecorder.Current = _previous;
            StateReadRecorder.TransactionHash = _previousTransactionHash;
        }
    }

    public static class StateReadRecorder
    {
        private static readonly AsyncLocal<BlockReadRecorder?> CurrentRecorder = new();
        private static readonly AsyncLocal<UInt256?> CurrentTransaction = new();
        private static readonly AsyncLocal<int> RecordingSuppressed = new();

        internal static BlockReadRecorder? Current
        {
            get => CurrentRecorder.Value;
            set => CurrentRecorder.Value = value;
        }

        internal static UInt256? TransactionHash
        {
            get => CurrentTransaction.Value;
            set => CurrentTransaction.Value = value;
        }

        private static bool IsRecordingSuppressed => RecordingSuppressed.Value > 0;

        public static bool Enabled => StateRecorderSettings.Current.Enabled;

        public static BlockReadRecorderScope? TryBegin(Block block)
        {
            if (!Enabled) return null;
            var recorder = new BlockReadRecorder(block);
            return new BlockReadRecorderScope(recorder, Current);
        }

        /// <summary>
        /// Begin a transaction scope for recording reads with transaction context.
        /// </summary>
        internal static IDisposable? BeginTransaction(UInt256? txHash)
        {
            if (!Enabled) return null;
            return new TransactionScope(txHash);
        }

        /// <summary>
        /// Record a storage read. Only the first read of each key per block is recorded.
        /// </summary>
        public static void Record(IReadOnlyStore? store, StorageKey key, StorageItem value, string source, UInt256? txHash = null)
        {
            if (IsRecordingSuppressed) return;
            var recorder = Current;
            if (recorder is null) return;

            txHash ??= TransactionHash;
            recorder.TryAdd(store, key, value, source, txHash);
        }

        /// <summary>
        /// Suppress recording temporarily (used when querying contract metadata to avoid recursion).
        /// </summary>
        internal static IDisposable SuppressRecordingScope()
        {
            RecordingSuppressed.Value = RecordingSuppressed.Value + 1;
            return new RecordingSuppressionScope();
        }

        private sealed class TransactionScope : IDisposable
        {
            private readonly UInt256? _previous;

            public TransactionScope(UInt256? txHash)
            {
                _previous = TransactionHash;
                TransactionHash = txHash;
            }

            public void Dispose()
            {
                TransactionHash = _previous;
            }
        }

        private sealed class RecordingSuppressionScope : IDisposable
        {
            public void Dispose()
            {
                var current = RecordingSuppressed.Value - 1;
                RecordingSuppressed.Value = current < 0 ? 0 : current;
            }
        }
    }

    public sealed class StateRecorderSettings
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
        /// When true, also upload per-block JSON/CSV exports to storage.
        /// Disabled by default to avoid creating large numbers of files.
        /// </summary>
        public bool UploadAuxFormats { get; init; }
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
                UploadAuxFormats = GetEnvBool("UPLOAD_AUX_FORMATS")
            };
        }

        private static UploadMode ParseUploadMode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return UploadMode.Binary;

            return Enum.TryParse(value, true, out UploadMode mode) ? mode : UploadMode.Binary;
        }

        private static ExecutionTraceLevel ParseTraceLevel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ExecutionTraceLevel.All;

            // Support comma-separated lists of flags (e.g., "Syscalls,OpCodes")
            var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                ExecutionTraceLevel combined = ExecutionTraceLevel.None;
                foreach (var part in parts)
                {
                    if (Enum.TryParse(part.Trim(), true, out ExecutionTraceLevel parsed))
                        combined |= parsed;
                }
                return combined == ExecutionTraceLevel.None ? ExecutionTraceLevel.All : combined;
            }

            return Enum.TryParse(value, true, out ExecutionTraceLevel mode) ? mode : ExecutionTraceLevel.All;
        }

        private static bool GetEnvBool(string name)
        {
            var value = Environment.GetEnvironmentVariable($"{Prefix}{name}");
            return value != null && bool.TryParse(value, out var result) ? result : false;
        }
    }
}
