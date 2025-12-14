// Copyright (C) 2015-2025 The Neo Project.
//
// RecordingStoreProvider.RecordingStore.cs file belongs to the neo project and is free
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
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class RecordingStoreProvider
    {
        private sealed partial class RecordingStore : IStore, IReadOnlyStore
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
                    RecordingStoreProvider.RecordReadBytes(this, key, value, nameof(TryGet));
                return success;
            }

            public bool Contains(byte[] key)
            {
                if (!StateReadRecorder.IsRecording)
                    return _inner.Contains(key);

                if (_inner.TryGet(key, out var value) && value != null)
                {
                    RecordingStoreProvider.RecordReadBytes(this, key, value, nameof(Contains));
                    return true;
                }

                return false;
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            {
                foreach (var (key, value) in _inner.Find(key_prefix, direction))
                {
                    RecordingStoreProvider.RecordReadBytes(this, key, value, nameof(Find));
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
        }
    }
}
