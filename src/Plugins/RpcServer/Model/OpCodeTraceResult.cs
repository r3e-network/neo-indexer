// Copyright (C) 2015-2025 The Neo Project.
//
// OpCodeTraceResult.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Json;
using System.Text.Json.Serialization;

namespace Neo.Plugins.RpcServer.Model
{
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
}

