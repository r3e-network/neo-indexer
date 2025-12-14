// Copyright (C) 2015-2025 The Neo Project.
//
// RecordingStoreProvider.RecordingStoreSnapshot.StorageKey.cs file belongs to the neo project and is free
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class RecordingStoreProvider
    {
        private sealed partial class RecordingStoreSnapshot
        {
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
                    RecordingStoreProvider.RecordReadItem(this, key, value, nameof(TryGet));
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
        }
    }
}
