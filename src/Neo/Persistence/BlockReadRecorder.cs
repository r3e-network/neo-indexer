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
using System;
using System.Collections.Generic;

namespace Neo.Persistence
{
    public sealed partial class BlockReadRecorder
    {
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
    }
}
