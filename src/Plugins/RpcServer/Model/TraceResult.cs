// Copyright (C) 2015-2025 The Neo Project.
//
// TraceResult.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Neo.Plugins.RpcServer.Model
{
    /// <summary>
    /// Aggregated trace payload returned by trace RPC endpoints.
    /// </summary>
    public sealed class TraceResult
    {
        public uint BlockIndex { get; init; }
        public string? BlockHash { get; init; }
        public string? TransactionHash { get; init; }
        public IReadOnlyList<OpCodeTraceResult> OpCodeTraces { get; init; } = Array.Empty<OpCodeTraceResult>();
        public IReadOnlyList<SyscallTraceResult> SyscallTraces { get; init; } = Array.Empty<SyscallTraceResult>();
        public IReadOnlyList<ContractCallResult> ContractCalls { get; init; } = Array.Empty<ContractCallResult>();
        public int Limit { get; init; }
        public int Offset { get; init; }
        public int OpCodeTotal { get; init; }
        public int SyscallTotal { get; init; }
        public int ContractCallTotal { get; init; }

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = (int)BlockIndex;
            if (!string.IsNullOrEmpty(BlockHash))
                json["blockHash"] = BlockHash;
            if (!string.IsNullOrEmpty(TransactionHash))
                json["transactionHash"] = TransactionHash;
            json["limit"] = Limit;
            json["offset"] = Offset;

            json["opcodes"] = BuildCollection(OpCodeTraces.Select(t => t.ToJson()), OpCodeTotal);
            json["syscalls"] = BuildCollection(SyscallTraces.Select(t => t.ToJson()), SyscallTotal);
            json["contractCalls"] = BuildCollection(ContractCalls.Select(t => t.ToJson()), ContractCallTotal);
            return json;
        }

        private static JObject BuildCollection(IEnumerable<JToken> items, int total)
        {
            var array = new JArray();
            foreach (var token in items)
            {
                array.Add(token);
            }

            JObject wrapper = new();
            wrapper["total"] = total;
            wrapper["items"] = array;
            return wrapper;
        }
    }

    /// <summary>
    /// Represents a single opcode trace row from Supabase.
    /// </summary>
    public sealed class OpCodeTraceResult
    {
        [JsonPropertyName("block_index")]
        public int BlockIndex { get; set; }

        [JsonPropertyName("tx_hash")]
        public string TransactionHash { get; set; } = string.Empty;

        [JsonPropertyName("contract_hash")]
        public string ContractHash { get; set; } = string.Empty;

        [JsonPropertyName("instruction_pointer")]
        public int InstructionPointer { get; set; }

        [JsonPropertyName("opcode")]
        public int OpCode { get; set; }

        [JsonPropertyName("opcode_name")]
        public string OpCodeName { get; set; } = string.Empty;

        [JsonPropertyName("operand_base64")]
        public string? OperandBase64 { get; set; }

        [JsonPropertyName("gas_consumed")]
        public long GasConsumed { get; set; }

        [JsonPropertyName("stack_depth")]
        public int? StackDepth { get; set; }

        [JsonPropertyName("trace_order")]
        public int TraceOrder { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = BlockIndex;
            json["transactionHash"] = TransactionHash;
            json["contractHash"] = ContractHash;
            json["instructionPointer"] = InstructionPointer;
            json["opcode"] = OpCode;
            json["opcodeName"] = OpCodeName;
            if (!string.IsNullOrEmpty(OperandBase64))
                json["operand"] = OperandBase64;
            json["gasConsumed"] = GasConsumed;
            if (StackDepth.HasValue)
                json["stackDepth"] = StackDepth.Value;
            json["traceOrder"] = TraceOrder;
            return json;
        }
    }

    /// <summary>
    /// Represents a syscall trace row from Supabase.
    /// </summary>
    public sealed class SyscallTraceResult
    {
        [JsonPropertyName("block_index")]
        public int BlockIndex { get; set; }

        [JsonPropertyName("tx_hash")]
        public string TransactionHash { get; set; } = string.Empty;

        [JsonPropertyName("contract_hash")]
        public string ContractHash { get; set; } = string.Empty;

        [JsonPropertyName("syscall_hash")]
        public string SyscallHash { get; set; } = string.Empty;

        [JsonPropertyName("syscall_name")]
        public string SyscallName { get; set; } = string.Empty;

        [JsonPropertyName("gas_cost")]
        public long GasCost { get; set; }

        [JsonPropertyName("trace_order")]
        public int TraceOrder { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = BlockIndex;
            json["transactionHash"] = TransactionHash;
            json["contractHash"] = ContractHash;
            json["syscallHash"] = SyscallHash;
            json["syscallName"] = SyscallName;
            json["gasCost"] = GasCost;
            json["traceOrder"] = TraceOrder;
            return json;
        }
    }

    /// <summary>
    /// Represents a contract call trace row.
    /// </summary>
    public sealed class ContractCallResult
    {
        [JsonPropertyName("block_index")]
        public int BlockIndex { get; set; }

        [JsonPropertyName("tx_hash")]
        public string TransactionHash { get; set; } = string.Empty;

        [JsonPropertyName("caller_hash")]
        public string? CallerHash { get; set; }

        [JsonPropertyName("callee_hash")]
        public string CalleeHash { get; set; } = string.Empty;

        [JsonPropertyName("method_name")]
        public string? MethodName { get; set; }

        [JsonPropertyName("call_depth")]
        public int CallDepth { get; set; }

        [JsonPropertyName("trace_order")]
        public int TraceOrder { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("gas_consumed")]
        public long? GasConsumed { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = BlockIndex;
            json["transactionHash"] = TransactionHash;
            if (!string.IsNullOrEmpty(CallerHash))
                json["callerHash"] = CallerHash;
            json["calleeHash"] = CalleeHash;
            if (!string.IsNullOrEmpty(MethodName))
                json["methodName"] = MethodName;
            json["callDepth"] = CallDepth;
            json["traceOrder"] = TraceOrder;
            json["success"] = Success;
            if (GasConsumed.HasValue)
                json["gasConsumed"] = GasConsumed.Value;
            return json;
        }
    }

    /// <summary>
    /// Aggregated syscall statistics row.
    /// </summary>
    public sealed class SyscallStatsResult
    {
        [JsonPropertyName("syscall_hash")]
        public string? SyscallHash { get; set; }

        [JsonPropertyName("syscall_name")]
        public string SyscallName { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("call_count")]
        public long CallCount { get; set; }

        [JsonPropertyName("total_gas_cost")]
        public long? TotalGasCost { get; set; }

        [JsonPropertyName("avg_gas_cost")]
        public double? AverageGasCost { get; set; }

        [JsonPropertyName("min_gas_cost")]
        public long? MinGasCost { get; set; }

        [JsonPropertyName("max_gas_cost")]
        public long? MaxGasCost { get; set; }

        [JsonPropertyName("first_block")]
        public int? FirstBlockIndex { get; set; }

        [JsonPropertyName("last_block")]
        public int? LastBlockIndex { get; set; }

        [JsonPropertyName("gas_base")]
        public long? GasBase { get; set; }

        [JsonPropertyName("total_rows")]
        public long? TotalRows { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["syscallName"] = SyscallName;
            if (!string.IsNullOrEmpty(SyscallHash))
                json["syscallHash"] = SyscallHash;
            json["callCount"] = CallCount;
            if (!string.IsNullOrEmpty(Category))
                json["category"] = Category;
            if (TotalGasCost.HasValue)
                json["totalGasCost"] = TotalGasCost.Value;
            if (AverageGasCost.HasValue)
                json["averageGasCost"] = AverageGasCost.Value;
            if (MinGasCost.HasValue)
                json["minGasCost"] = MinGasCost.Value;
            if (MaxGasCost.HasValue)
                json["maxGasCost"] = MaxGasCost.Value;
            if (FirstBlockIndex.HasValue)
                json["firstBlock"] = FirstBlockIndex.Value;
            if (LastBlockIndex.HasValue)
                json["lastBlock"] = LastBlockIndex.Value;
            if (GasBase.HasValue)
                json["gasBase"] = GasBase.Value;
            return json;
        }
    }

    /// <summary>
    /// Aggregated opcode statistics row.
    /// </summary>
    public sealed class OpCodeStatsResult
    {
        [JsonPropertyName("opcode")]
        public int OpCode { get; set; }

        [JsonPropertyName("opcode_name")]
        public string OpCodeName { get; set; } = string.Empty;

        [JsonPropertyName("call_count")]
        public long CallCount { get; set; }

        [JsonPropertyName("total_gas_consumed")]
        public long? TotalGasConsumed { get; set; }

        [JsonPropertyName("avg_gas_consumed")]
        public double? AverageGasConsumed { get; set; }

        [JsonPropertyName("min_gas_consumed")]
        public long? MinGasConsumed { get; set; }

        [JsonPropertyName("max_gas_consumed")]
        public long? MaxGasConsumed { get; set; }

        [JsonPropertyName("first_block")]
        public int? FirstBlockIndex { get; set; }

        [JsonPropertyName("last_block")]
        public int? LastBlockIndex { get; set; }

        [JsonPropertyName("total_rows")]
        public long? TotalRows { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["opcode"] = OpCode;
            json["opcodeName"] = OpCodeName;
            json["callCount"] = CallCount;
            if (TotalGasConsumed.HasValue)
                json["totalGasConsumed"] = TotalGasConsumed.Value;
            if (AverageGasConsumed.HasValue)
                json["averageGasConsumed"] = AverageGasConsumed.Value;
            if (MinGasConsumed.HasValue)
                json["minGasConsumed"] = MinGasConsumed.Value;
            if (MaxGasConsumed.HasValue)
                json["maxGasConsumed"] = MaxGasConsumed.Value;
            if (FirstBlockIndex.HasValue)
                json["firstBlock"] = FirstBlockIndex.Value;
            if (LastBlockIndex.HasValue)
                json["lastBlock"] = LastBlockIndex.Value;
            return json;
        }
    }

    /// <summary>
    /// Aggregated contract/native call statistics row.
    /// </summary>
    public sealed class ContractCallStatsResult
    {
        [JsonPropertyName("callee_hash")]
        public string CalleeHash { get; set; } = string.Empty;

        [JsonPropertyName("caller_hash")]
        public string? CallerHash { get; set; }

        [JsonPropertyName("method_name")]
        public string? MethodName { get; set; }

        [JsonPropertyName("call_count")]
        public long CallCount { get; set; }

        [JsonPropertyName("success_count")]
        public long? SuccessCount { get; set; }

        [JsonPropertyName("failure_count")]
        public long? FailureCount { get; set; }

        [JsonPropertyName("total_gas_consumed")]
        public long? TotalGasConsumed { get; set; }

        [JsonPropertyName("avg_gas_consumed")]
        public double? AverageGasConsumed { get; set; }

        [JsonPropertyName("first_block")]
        public int? FirstBlockIndex { get; set; }

        [JsonPropertyName("last_block")]
        public int? LastBlockIndex { get; set; }

        [JsonPropertyName("total_rows")]
        public long? TotalRows { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["calleeHash"] = CalleeHash;
            if (!string.IsNullOrEmpty(CallerHash))
                json["callerHash"] = CallerHash;
            if (!string.IsNullOrEmpty(MethodName))
                json["methodName"] = MethodName;
            json["callCount"] = CallCount;
            if (SuccessCount.HasValue)
                json["successCount"] = SuccessCount.Value;
            if (FailureCount.HasValue)
                json["failureCount"] = FailureCount.Value;
            if (TotalGasConsumed.HasValue)
                json["totalGasConsumed"] = TotalGasConsumed.Value;
            if (AverageGasConsumed.HasValue)
                json["averageGasConsumed"] = AverageGasConsumed.Value;
            if (FirstBlockIndex.HasValue)
                json["firstBlock"] = FirstBlockIndex.Value;
            if (LastBlockIndex.HasValue)
                json["lastBlock"] = LastBlockIndex.Value;
            return json;
        }
    }
}
