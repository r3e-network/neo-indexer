// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RecordBuilders.Contracts.cs file belongs to the neo project and is free
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
        private static List<ContractRecord> BuildContractRecords(BlockReadEntry[] entries)
        {
            var records = new List<ContractRecord>();
            var seen = new HashSet<int>();
            foreach (var entry in entries)
            {
                var contractId = entry.Key.Id;
                if (!seen.Add(contractId)) continue; // Skip duplicates in this block
                if (ContractCache.ContainsKey(contractId)) continue; // Skip already cached

                records.Add(new ContractRecord(contractId, entry.ContractHash.ToString(), entry.ManifestName));
            }
            return records;
        }
    }
}

