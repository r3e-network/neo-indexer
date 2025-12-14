// Copyright (C) 2015-2025 The Neo Project.
//
// SyscallStatsResult.cs file belongs to the neo project and is free
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
}

