// Copyright (C) 2015-2025 The Neo Project.
//
// TransactionResultResult.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Json;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neo.Plugins.RpcServer.Model
{
    /// <summary>
    /// Represents a transaction_results row from Supabase.
    /// </summary>
    public sealed class TransactionResultResult
    {
        [JsonPropertyName("block_index")]
        public int BlockIndex { get; set; }

        [JsonPropertyName("tx_hash")]
        public string TransactionHash { get; set; } = string.Empty;

        [JsonPropertyName("vm_state")]
        public int VmState { get; set; }

        [JsonPropertyName("vm_state_name")]
        public string VmStateName { get; set; } = string.Empty;

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("gas_consumed")]
        public long GasConsumed { get; set; }

        [JsonPropertyName("fault_exception")]
        public string? FaultException { get; set; }

        [JsonPropertyName("result_stack_json")]
        public JsonElement? ResultStackJson { get; set; }

        [JsonPropertyName("opcode_count")]
        public int OpCodeCount { get; set; }

        [JsonPropertyName("syscall_count")]
        public int SyscallCount { get; set; }

        [JsonPropertyName("contract_call_count")]
        public int ContractCallCount { get; set; }

        [JsonPropertyName("storage_write_count")]
        public int StorageWriteCount { get; set; }

        [JsonPropertyName("notification_count")]
        public int NotificationCount { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = BlockIndex;
            json["transactionHash"] = TransactionHash;
            json["vmState"] = VmState;
            json["vmStateName"] = VmStateName;
            json["success"] = Success;
            json["gasConsumed"] = GasConsumed;

            if (!string.IsNullOrEmpty(FaultException))
                json["faultException"] = FaultException;

            var stack = TryParseResultStack(ResultStackJson);
            if (stack is not null)
                json["resultStack"] = stack;

            json["opcodeCount"] = OpCodeCount;
            json["syscallCount"] = SyscallCount;
            json["contractCallCount"] = ContractCallCount;
            json["storageWriteCount"] = StorageWriteCount;
            json["notificationCount"] = NotificationCount;
            return json;
        }

        private static JToken? TryParseResultStack(JsonElement? element)
        {
            if (!element.HasValue)
                return null;

            try
            {
                var raw = element.Value.GetRawText();
                if (string.IsNullOrWhiteSpace(raw))
                    return null;
                return JToken.Parse(raw);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

