// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.Builders.Syscalls.cs file belongs to the neo project and is free
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
        private static List<SyscallTraceRow> BuildSyscallTraceRows(
            int blockIndex,
            string txHash,
            IReadOnlyList<SyscallTrace> traces,
            Dictionary<UInt160, string> contractHashCache)
        {
            var rows = new List<SyscallTraceRow>(traces.Count);
            foreach (var trace in traces)
            {
                var contractHash = trace.ContractHash ?? UInt160.Zero;
                var contractHashString = GetContractHashString(contractHash, contractHashCache);
                rows.Add(new SyscallTraceRow(
                    blockIndex,
                    txHash,
                    trace.Order,
                    contractHashString,
                    trace.SyscallHash,
                    trace.SyscallName,
                    trace.GasCost,
                    trace.Success));
            }
            return rows;
        }
    }
}
