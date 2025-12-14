// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.Builders.OpCodes.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Generic;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static List<OpCodeTraceRow> BuildOpCodeTraceRows(
            int blockIndex,
            string txHash,
            IReadOnlyList<OpCodeTrace> traces,
            Dictionary<UInt160, string> contractHashCache)
        {
            var rows = new List<OpCodeTraceRow>(traces.Count);
            foreach (var trace in traces)
            {
                var operand = trace.Operand.IsEmpty ? null : Convert.ToBase64String(trace.Operand.Span);
                var contractHash = trace.ContractHash ?? UInt160.Zero;
                var contractHashString = GetContractHashString(contractHash, contractHashCache);
                var opCodeName = GetOpCodeName(trace.OpCode);
                rows.Add(new OpCodeTraceRow(
                    blockIndex,
                    txHash,
                    trace.Order,
                    contractHashString,
                    trace.InstructionPointer,
                    (int)trace.OpCode,
                    opCodeName,
                    operand,
                    trace.GasConsumed,
                    trace.StackDepth));
            }
            return rows;
        }
    }
}

