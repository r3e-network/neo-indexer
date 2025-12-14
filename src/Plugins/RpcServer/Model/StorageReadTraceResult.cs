// Copyright (C) 2015-2025 The Neo Project.
//
// StorageReadTraceResult.cs file belongs to the neo project and is free
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
    public sealed class ContractMetadataResult
    {
        [JsonPropertyName("contract_hash")]
        public string ContractHash { get; set; } = string.Empty;

        [JsonPropertyName("manifest_name")]
        public string? ManifestName { get; set; }
    }

    /// <summary>
    /// Represents a storage_reads row from Supabase.
    /// </summary>
    public sealed class StorageReadTraceResult
    {
        [JsonPropertyName("block_index")]
        public int BlockIndex { get; set; }

        [JsonPropertyName("tx_hash")]
        public string? TransactionHash { get; set; }

        [JsonPropertyName("read_order")]
        public int ReadOrder { get; set; }

        [JsonPropertyName("contract_id")]
        public int? ContractId { get; set; }

        [JsonPropertyName("key_base64")]
        public string KeyBase64 { get; set; } = string.Empty;

        [JsonPropertyName("value_base64")]
        public string ValueBase64 { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("contracts")]
        public ContractMetadataResult? Contract { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = BlockIndex;
            if (!string.IsNullOrEmpty(TransactionHash))
                json["transactionHash"] = TransactionHash;
            json["readOrder"] = ReadOrder;
            if (ContractId.HasValue)
                json["contractId"] = ContractId.Value;
            if (Contract != null && !string.IsNullOrEmpty(Contract.ContractHash))
                json["contractHash"] = Contract.ContractHash;
            if (Contract != null && !string.IsNullOrEmpty(Contract.ManifestName))
                json["manifestName"] = Contract.ManifestName;
            json["keyBase64"] = KeyBase64;
            json["valueBase64"] = ValueBase64;
            if (!string.IsNullOrEmpty(Source))
                json["source"] = Source;
            return json;
        }
    }
}
