// Copyright (C) 2015-2025 The Neo Project.
//
// StorageWriteTraceResult.cs file belongs to the neo project and is free
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
    /// Represents a storage_writes trace row from Supabase.
    /// </summary>
    public sealed class StorageWriteTraceResult
    {
        [JsonPropertyName("block_index")]
        public int BlockIndex { get; set; }

        [JsonPropertyName("tx_hash")]
        public string TransactionHash { get; set; } = string.Empty;

        [JsonPropertyName("write_order")]
        public int WriteOrder { get; set; }

        [JsonPropertyName("contract_id")]
        public int? ContractId { get; set; }

        [JsonPropertyName("contract_hash")]
        public string ContractHash { get; set; } = string.Empty;

        [JsonPropertyName("key_base64")]
        public string KeyBase64 { get; set; } = string.Empty;

        [JsonPropertyName("old_value_base64")]
        public string? OldValueBase64 { get; set; }

        [JsonPropertyName("new_value_base64")]
        public string NewValueBase64 { get; set; } = string.Empty;

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = BlockIndex;
            json["transactionHash"] = TransactionHash;
            json["writeOrder"] = WriteOrder;
            if (ContractId.HasValue)
                json["contractId"] = ContractId.Value;
            json["contractHash"] = ContractHash;
            json["keyBase64"] = KeyBase64;
            if (!string.IsNullOrEmpty(OldValueBase64))
                json["oldValueBase64"] = OldValueBase64;
            json["newValueBase64"] = NewValueBase64;
            return json;
        }
    }
}

