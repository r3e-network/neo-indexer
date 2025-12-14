// Copyright (C) 2015-2025 The Neo Project.
//
// RuntimeLogTraceResult.cs file belongs to the neo project and is free
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
    /// Represents a runtime_logs trace row from Supabase.
    /// </summary>
    public sealed class RuntimeLogTraceResult
    {
        [JsonPropertyName("block_index")]
        public int BlockIndex { get; set; }

        [JsonPropertyName("tx_hash")]
        public string TransactionHash { get; set; } = string.Empty;

        [JsonPropertyName("log_order")]
        public int LogOrder { get; set; }

        [JsonPropertyName("contract_hash")]
        public string ContractHash { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = BlockIndex;
            json["transactionHash"] = TransactionHash;
            json["logOrder"] = LogOrder;
            json["contractHash"] = ContractHash;
            json["message"] = Message;
            return json;
        }
    }
}

