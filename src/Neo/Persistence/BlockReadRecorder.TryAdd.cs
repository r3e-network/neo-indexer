// Copyright (C) 2015-2025 The Neo Project.
//
// BlockReadRecorder.TryAdd.cs file belongs to the neo project and is free
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
using System.Threading;

namespace Neo.Persistence
{
    public sealed partial class BlockReadRecorder
    {
        /// <summary>
        /// Try to add a storage read entry. Only the FIRST read of each key is recorded.
        /// Thread-safe for concurrent transaction execution within a block.
        /// </summary>
        public bool TryAdd(IReadOnlyStore? store, StorageKey key, StorageItem value, string? source, UInt256? txHash)
        {
            if (_isFull)
            {
                Interlocked.Increment(ref _droppedEntries);
                return false;
            }

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
    }
}
