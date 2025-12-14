// Copyright (C) 2015-2025 The Neo Project.
//
// ContractCallResult.cs file belongs to the neo project and is free
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
}

