// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.Builders.ContractCalls.cs file belongs to the neo project and is free
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
        private static List<ContractCallTraceRow> BuildContractCallTraceRows(
            int blockIndex,
            string txHash,
            IReadOnlyList<ContractCallTrace> traces,
            Dictionary<UInt160, string> contractHashCache)
        {
            var rows = new List<ContractCallTraceRow>(traces.Count);
            foreach (var trace in traces)
            {
                var calleeHash = trace.CalleeHash ?? UInt160.Zero;
                var calleeHashString = GetContractHashString(calleeHash, contractHashCache);
                rows.Add(new ContractCallTraceRow(
                    blockIndex,
                    txHash,
                    trace.Order,
                    GetContractHashStringOrNull(trace.CallerHash, contractHashCache),
                    calleeHashString,
                    trace.MethodName,
                    trace.CallDepth,
                    trace.Success,
                    trace.GasConsumed));
            }
            return rows;
        }
    }
}

