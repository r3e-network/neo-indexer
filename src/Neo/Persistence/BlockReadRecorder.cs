// Copyright (C) 2015-2025 The Neo Project.
//
// BlockReadRecorder.cs file belongs to the neo project and is free
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
        private readonly int _maxEntries;
        private int _droppedEntries;
        private bool _isFull;
        private int _order;

        public uint BlockIndex { get; }
        public UInt256 BlockHash { get; }
        public ulong Timestamp { get; }
        public int TransactionCount { get; }
        public IReadOnlyCollection<BlockReadEntry> Entries => _entries;
        public bool IsFull => _isFull;
        public int DroppedEntries => _droppedEntries;

        public BlockReadRecorder(Block block, int maxEntries = 0)
        {
            BlockIndex = block.Index;
            BlockHash = block.Hash;
            Timestamp = block.Timestamp;
            TransactionCount = block.Transactions.Length;
            _maxEntries = maxEntries;
        }

        /// <summary>
        /// Try to add a storage read entry. Only the FIRST read of each key is recorded.
        /// Thread-safe for concurrent transaction execution within a block.
        /// </summary>
        public bool TryAdd(IReadOnlyStore? store, StorageKey key, StorageItem value, string? source, UInt256? txHash)
        {
            if (_isFull) return false;

            int order = 0;
            bool accepted = false;
            bool shouldLogCapped = false;
            // First-read deduplication: check if key already recorded
            lock (_entryLock)
            {
                if (_maxEntries > 0 && _order >= _maxEntries)
                {
                    if (!_isFull)
                    {
                        _isFull = true;
                        shouldLogCapped = true;
                    }

                    _droppedEntries++;
                }
                else
                {
                    if (!_readKeys.Add(key)) return false;
                    order = ++_order;
                    accepted = true;
                    if (_maxEntries > 0 && _order >= _maxEntries && !_isFull)
                    {
                        _isFull = true;
                        shouldLogCapped = true;
                    }
                }
            }

            if (shouldLogCapped)
            {
                Utility.Log(nameof(BlockReadRecorder), LogLevel.Warning,
                    $"Block {BlockIndex}: storage read recording reached cap ({_maxEntries}). Further reads will be ignored.");
            }

            if (!accepted)
                return false;

            // Resolve contract metadata (outside lock to avoid blocking)
            var metadata = GetContractMetadata(store, key.Id);
            var entry = new BlockReadEntry(
                key,
                value.Clone(),
                order,
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
}

