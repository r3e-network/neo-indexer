// Copyright (C) 2015-2025 The Neo Project.
//
// StorageWriteStatsResult.cs file belongs to the neo project and is free
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
    /// Aggregated storage write statistics row.
    /// </summary>
    public sealed class StorageWriteStatsResult
    {
        [JsonPropertyName("contract_hash")]
        public string ContractHash { get; set; } = string.Empty;

        [JsonPropertyName("write_count")]
        public long WriteCount { get; set; }

        [JsonPropertyName("delete_count")]
        public long DeleteCount { get; set; }

        [JsonPropertyName("first_block")]
        public int? FirstBlockIndex { get; set; }

        [JsonPropertyName("last_block")]
        public int? LastBlockIndex { get; set; }

        [JsonPropertyName("total_rows")]
        public long? TotalRows { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["contractHash"] = ContractHash;
            json["writeCount"] = WriteCount;
            json["deleteCount"] = DeleteCount;
            if (FirstBlockIndex.HasValue)
                json["firstBlock"] = FirstBlockIndex.Value;
            if (LastBlockIndex.HasValue)
                json["lastBlock"] = LastBlockIndex.Value;
            return json;
        }
    }
}

