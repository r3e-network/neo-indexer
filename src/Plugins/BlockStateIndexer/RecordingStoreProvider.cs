// Copyright (C) 2015-2025 The Neo Project.
//
// RecordingStoreProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;
using Neo.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Neo.SmartContract;

namespace Neo.Plugins.BlockStateIndexer
{
    /// <summary>
    /// Storage provider that wraps another provider to record storage reads.
    /// Configure Neo to use provider name "RecordingStore" and set
    /// NEO_STATE_RECORDER__BASE_STORE_PROVIDER to the underlying provider name
    /// (e.g. "LevelDBStore").
    /// </summary>
    public sealed class RecordingStoreProvider : Plugin, IStoreProvider
    {
        private const string BaseProviderEnvVar = "NEO_STATE_RECORDER__BASE_STORE_PROVIDER";
        private readonly string _baseProviderName;

        public override string Name => "RecordingStore";
        public override string Description => "Wraps another IStoreProvider and records storage reads for indexing.";

        public RecordingStoreProvider()
        {
            _baseProviderName = Environment.GetEnvironmentVariable(BaseProviderEnvVar) ?? "LevelDBStore";
            StoreFactory.RegisterProvider(this);
        }

        public IStore GetStore(string path)
        {
            var baseProvider = StoreFactory.GetStoreProvider(_baseProviderName);
            if (baseProvider is null)
                throw new ArgumentException($"Can't find base storage provider '{_baseProviderName}'.");
            if (ReferenceEquals(baseProvider, this))
                throw new InvalidOperationException("RecordingStoreProvider cannot wrap itself.");

            var store = baseProvider.GetStore(path);
            return new RecordingStore(store);
        }

        private sealed class RecordingStore : IStore, IReadOnlyStore
        {
            private readonly IStore _inner;

            public RecordingStore(IStore inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            #region IReadOnlyStore<byte[], byte[]>

            public byte[]? TryGet(byte[] key)
            {
                return TryGet(key, out var value) ? value : null;
            }

            public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
            {
                var success = _inner.TryGet(key, out value);
                if (success && value != null)
                    RecordReadBytes(key, value, nameof(TryGet));
                return success;
            }

            public bool Contains(byte[] key)
            {
                var exists = _inner.Contains(key);
                if (!StateReadRecorder.Enabled)
                    return exists;

                if (_inner.TryGet(key, out var value) && value != null)
                {
                    RecordReadBytes(key, value, nameof(Contains));
                    return true;
                }

                return false;
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            {
                foreach (var (key, value) in _inner.Find(key_prefix, direction))
                {
                    RecordReadBytes(key, value, nameof(Find));
                    yield return (key, value);
                }
            }

            #endregion

            #region IWriteStore<byte[], byte[]>

            public void Put(byte[] key, byte[] value) => _inner.Put(key, value);

            public void Delete(byte[] key) => _inner.Delete(key);

            #endregion

            public IStoreSnapshot GetSnapshot()
            {
                return new RecordingStoreSnapshot(_inner.GetSnapshot());
            }

            public void Dispose()
            {
                _inner.Dispose();
            }

            #region IReadOnlyStore (StorageKey, StorageItem)

            public StorageItem? TryGet(StorageKey key)
            {
                return TryGet(key, out var item) ? item : null;
            }

            public bool TryGet(StorageKey key, [NotNullWhen(true)] out StorageItem? value)
            {
                if (_inner.TryGet(key.ToArray(), out var bytes) && bytes != null)
                {
                    value = new StorageItem(bytes);
                    RecordReadItem(key, value, nameof(TryGet));
                    return true;
                }

                value = null;
                return false;
            }

            public bool Contains(StorageKey key)
            {
                return Contains(key.ToArray());
            }

            public IEnumerable<(StorageKey Key, StorageItem Value)> Find(StorageKey? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            {
                var prefixBytes = key_prefix?.ToArray();
                foreach (var (key, value) in Find(prefixBytes, direction))
                {
                    StorageKey storageKey = key;
                    yield return (storageKey, new StorageItem(value));
                }
            }

            #endregion

            private void RecordReadBytes(byte[] keyBytes, byte[] valueBytes, string source)
            {
                if (!StateReadRecorder.Enabled)
                    return;

                StorageKey storageKey = keyBytes;
                var storageItem = new StorageItem(valueBytes);
                StateReadRecorder.Record(this, storageKey, storageItem, source);
            }

            private void RecordReadItem(StorageKey key, StorageItem item, string source)
            {
                if (!StateReadRecorder.Enabled)
                    return;

                StateReadRecorder.Record(this, key, item, source);
            }
        }

        private sealed class RecordingStoreSnapshot : IStoreSnapshot, IReadOnlyStore
        {
            private readonly IStoreSnapshot _inner;

            public RecordingStoreSnapshot(IStoreSnapshot inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public IStore Store => _inner.Store;

            #region IReadOnlyStore<byte[], byte[]>

            public byte[]? TryGet(byte[] key)
            {
                return TryGet(key, out var value) ? value : null;
            }

            public bool TryGet(byte[] key, [NotNullWhen(true)] out byte[]? value)
            {
                var success = _inner.TryGet(key, out value);
                if (success && value != null)
                    RecordReadBytes(key, value, nameof(TryGet));
                return success;
            }

            public bool Contains(byte[] key)
            {
                var exists = _inner.Contains(key);
                if (!StateReadRecorder.Enabled)
                    return exists;

                if (_inner.TryGet(key, out var value) && value != null)
                {
                    RecordReadBytes(key, value, nameof(Contains));
                    return true;
                }

                return false;
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            {
                foreach (var (key, value) in _inner.Find(key_prefix, direction))
                {
                    RecordReadBytes(key, value, nameof(Find));
                    yield return (key, value);
                }
            }

            #endregion

            #region IWriteStore<byte[], byte[]>

            public void Put(byte[] key, byte[] value) => _inner.Put(key, value);

            public void Delete(byte[] key) => _inner.Delete(key);

            public void Commit() => _inner.Commit();

            #endregion

            public void Dispose()
            {
                _inner.Dispose();
            }

            #region IReadOnlyStore (StorageKey, StorageItem)

            public StorageItem? TryGet(StorageKey key)
            {
                return TryGet(key, out var item) ? item : null;
            }

            public bool TryGet(StorageKey key, [NotNullWhen(true)] out StorageItem? value)
            {
                if (_inner.TryGet(key.ToArray(), out var bytes) && bytes != null)
                {
                    value = new StorageItem(bytes);
                    RecordReadItem(key, value, nameof(TryGet));
                    return true;
                }

                value = null;
                return false;
            }

            public bool Contains(StorageKey key)
            {
                return Contains(key.ToArray());
            }

            public IEnumerable<(StorageKey Key, StorageItem Value)> Find(StorageKey? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            {
                var prefixBytes = key_prefix?.ToArray();
                foreach (var (key, value) in Find(prefixBytes, direction))
                {
                    StorageKey storageKey = key;
                    yield return (storageKey, new StorageItem(value));
                }
            }

            #endregion

            private void RecordReadBytes(byte[] keyBytes, byte[] valueBytes, string source)
            {
                if (!StateReadRecorder.Enabled)
                    return;

                StorageKey storageKey = keyBytes;
                var storageItem = new StorageItem(valueBytes);
                StateReadRecorder.Record(this, storageKey, storageItem, source);
            }

            private void RecordReadItem(StorageKey key, StorageItem item, string source)
            {
                if (!StateReadRecorder.Enabled)
                    return;

                StateReadRecorder.Record(this, key, item, source);
            }
        }
    }
}
