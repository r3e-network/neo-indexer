// Copyright (C) 2015-2025 The Neo Project.
//
// BlockReadRecorder.ContractMetadata.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.SmartContract;
using Neo.SmartContract.Native;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Persistence
{
    public sealed partial class BlockReadRecorder
    {
        private static readonly IReadOnlyDictionary<int, ContractMetadata> NativeContractsById =
            NativeContract.Contracts.ToDictionary(c => c.Id, c => new ContractMetadata(c.Hash, c.Name));

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

