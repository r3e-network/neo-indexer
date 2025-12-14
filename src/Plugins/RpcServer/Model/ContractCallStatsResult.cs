// Copyright (C) 2015-2025 The Neo Project.
//
// ContractCallStatsResult.cs file belongs to the neo project and is free
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

