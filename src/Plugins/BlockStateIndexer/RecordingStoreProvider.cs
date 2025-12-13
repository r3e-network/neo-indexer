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
    public sealed partial class RecordingStoreProvider : Plugin, IStoreProvider
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
    }
}
