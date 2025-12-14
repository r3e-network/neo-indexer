// Copyright (C) 2015-2025 The Neo Project.
//
// SyscallTraceResult.cs file belongs to the neo project and is free
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
}

