// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.Builders.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Extensions;
using Neo.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
using NpgsqlTypes;
#endif


namespace Neo.Persistence
{
	public static partial class StateRecorderSupabase
	{
		#region Trace Builders
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
	                    trace.GasCost));
	            }
	            return rows;
	        }

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

	        private static List<StorageWriteTraceRow> BuildStorageWriteTraceRows(
	            int blockIndex,
	            string txHash,
	            IReadOnlyList<StorageWriteTrace> traces,
	            Dictionary<UInt160, string> contractHashCache)
	        {
	            var rows = new List<StorageWriteTraceRow>(traces.Count);
	            foreach (var trace in traces)
	            {
	                var contractHash = trace.ContractHash ?? UInt160.Zero;
	                var contractHashString = GetContractHashString(contractHash, contractHashCache);
	                rows.Add(new StorageWriteTraceRow(
	                    blockIndex,
	                    txHash,
	                    trace.Order,
	                    trace.ContractId,
	                    contractHashString,
	                    Convert.ToBase64String(trace.Key.Span),
	                    trace.OldValue.HasValue ? Convert.ToBase64String(trace.OldValue.Value.Span) : null,
	                    Convert.ToBase64String(trace.NewValue.Span)));
	            }
	            return rows;
	        }

	        private static List<NotificationTraceRow> BuildNotificationTraceRows(
	            int blockIndex,
	            string txHash,
	            IReadOnlyList<NotificationTrace> traces,
	            Dictionary<UInt160, string> contractHashCache)
	        {
	            var rows = new List<NotificationTraceRow>(traces.Count);
	            foreach (var trace in traces)
	            {
	                var contractHash = trace.ContractHash ?? UInt160.Zero;
	                var contractHashString = GetContractHashString(contractHash, contractHashCache);
	                rows.Add(new NotificationTraceRow(
	                    blockIndex,
	                    txHash,
	                    trace.Order,
	                    contractHashString,
	                    trace.EventName,
	                    ParseNotificationState(trace.StateJson)));
	            }
	            return rows;
	        }

        private static JsonElement? ParseNotificationState(string? stateJson)
        {
            if (string.IsNullOrWhiteSpace(stateJson))
                return null;

            try
            {
                using var document = JsonDocument.Parse(stateJson);
                return document.RootElement.Clone();
            }
            catch (Exception)
            {
                return null;
            }
        }
		#endregion

	}
}
