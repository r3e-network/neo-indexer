// Copyright (C) 2015-2025 The Neo Project.
//
// OpCodeStatsResult.cs file belongs to the neo project and is free
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
}

