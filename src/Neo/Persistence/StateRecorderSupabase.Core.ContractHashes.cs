// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Core.ContractHashes.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Generic;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static string GetContractHashString(UInt160 contractHash, Dictionary<UInt160, string> cache)
        {
            if (!cache.TryGetValue(contractHash, out var value))
            {
                value = contractHash.ToString();
                cache[contractHash] = value;
            }
            return value;
        }

        private static string? GetContractHashStringOrNull(UInt160? contractHash, Dictionary<UInt160, string> cache)
        {
            if (contractHash is null) return null;
            return GetContractHashString(contractHash, cache);
        }
    }
}

