// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngine.StateReadRecorder.RecordingStoreSnapshot.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Neo.UnitTests.SmartContract
{
    public partial class UT_TracingApplicationEngine
    {
        private sealed class RecordingStoreSnapshot : IStoreSnapshot
        {
            private readonly IStoreSnapshot _inner;

            public RecordingStoreSnapshot(IStoreSnapshot inner)
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
                    RecordRead(key, value, nameof(TryGet));
                return success;
            }

            public bool Contains(byte[] key)
            {
                if (!StateReadRecorder.IsRecording)
                    return _inner.Contains(key);

                if (_inner.TryGet(key, out var value) && value != null)
                {
                    RecordRead(key, value, nameof(Contains));
                    return true;
                }

                return false;
            }

            public IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward)
            {
                foreach (var (key, value) in _inner.Find(key_prefix, direction))
                {
                    RecordRead(key, value, nameof(Find));
                    yield return (key, value);
                }
            }

            public void Put(byte[] key, byte[] value) => _inner.Put(key, value);

            public void Delete(byte[] key) => _inner.Delete(key);

            public void Commit() => _inner.Commit();

            public void Dispose() => _inner.Dispose();

            private static void RecordRead(byte[] keyBytes, byte[] valueBytes, string source)
            {
                if (!StateReadRecorder.IsRecording)
                    return;

                StorageKey storageKey = keyBytes;
                var storageItem = new StorageItem(valueBytes);
                StateReadRecorder.Record(store: null, storageKey, storageItem, source);
            }
        }
    }
}

