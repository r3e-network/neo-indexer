// Copyright (C) 2015-2025 The Neo Project.
//
// RecordingStoreProvider.ReadRecording.cs file belongs to the neo project and is free
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

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class RecordingStoreProvider
    {
        private static void RecordReadBytes(IReadOnlyStore store, byte[] keyBytes, byte[] valueBytes, string source)
        {
            if (!StateReadRecorder.IsRecording)
                return;

            StorageKey storageKey = keyBytes;
            var storageItem = new StorageItem(valueBytes);
            StateReadRecorder.Record(store, storageKey, storageItem, source);
        }

        private static void RecordReadItem(IReadOnlyStore store, StorageKey key, StorageItem item, string source)
        {
            if (!StateReadRecorder.IsRecording)
                return;

            StateReadRecorder.Record(store, key, item, source);
        }
    }
}

