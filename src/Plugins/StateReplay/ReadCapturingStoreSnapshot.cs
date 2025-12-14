// Copyright (C) 2015-2025 The Neo Project.
//
// ReadCapturingStoreSnapshot.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace StateReplay
{
    internal sealed class ReadCapturingStoreSnapshot : IStoreSnapshot
    {
        private readonly IStoreSnapshot _inner;
        private readonly HashSet<string> _hitKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _missKeys = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> HitKeys => _hitKeys;
        public IReadOnlyCollection<string> MissKeys => _missKeys;

        public ReadCapturingStoreSnapshot(IStoreSnapshot inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IStore Store => _inner.Store;

        public byte[]? TryGet(byte[] key)
        {
            return TryGet(key, out var value) ? value : null;
        }

        public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
        {
            var success = _inner.TryGet(key, out value);
            if (success && value != null)
            {
                _hitKeys.Add(Convert.ToBase64String(key));
            }
            else
            {
                _missKeys.Add(Convert.ToBase64String(key));
            }
            return success;
        }

        public bool Contains(byte[] key)
        {
            // Mirror indexer behavior: when recording, Contains becomes TryGet so we can attribute a read.
            // Only record hits here to avoid recording many existence-check misses as "missing snapshot keys".
            if (_inner.TryGet(key, out var value) && value != null)
            {
                _hitKeys.Add(Convert.ToBase64String(key));
                return true;
            }
            return false;
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
        {
            foreach (var (key, value) in _inner.Find(key_prefix, direction))
            {
                _hitKeys.Add(Convert.ToBase64String(key));
                yield return (key, value);
            }
        }

        public void Put(byte[] key, byte[] value) => _inner.Put(key, value);

        public void Delete(byte[] key) => _inner.Delete(key);

        public void Commit() => _inner.Commit();

        public void Dispose() => _inner.Dispose();
    }
}

