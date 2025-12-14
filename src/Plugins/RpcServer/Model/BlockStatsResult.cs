// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStatsResult.cs file belongs to the neo project and is free
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
    /// Aggregated per-block statistics row.
    /// </summary>
    public sealed class BlockStatsResult
    {
        [JsonPropertyName("block_index")]
        public int BlockIndex { get; set; }

        [JsonPropertyName("tx_count")]
        public int TransactionCount { get; set; }

        [JsonPropertyName("total_gas_consumed")]
        public long TotalGasConsumed { get; set; }

        [JsonPropertyName("opcode_count")]
        public int OpCodeCount { get; set; }

        [JsonPropertyName("syscall_count")]
        public int SyscallCount { get; set; }

        [JsonPropertyName("contract_call_count")]
        public int ContractCallCount { get; set; }

        [JsonPropertyName("storage_read_count")]
        public int StorageReadCount { get; set; }

        [JsonPropertyName("storage_write_count")]
        public int StorageWriteCount { get; set; }

        [JsonPropertyName("notification_count")]
        public int NotificationCount { get; set; }

        [JsonPropertyName("log_count")]
        public int LogCount { get; set; }

        [JsonPropertyName("total_rows")]
        public long? TotalRows { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = BlockIndex;
            json["transactionCount"] = TransactionCount;
            json["totalGasConsumed"] = TotalGasConsumed;
            json["opcodeCount"] = OpCodeCount;
            json["syscallCount"] = SyscallCount;
            json["contractCallCount"] = ContractCallCount;
            json["storageReadCount"] = StorageReadCount;
            json["storageWriteCount"] = StorageWriteCount;
            json["notificationCount"] = NotificationCount;
            json["logCount"] = LogCount;
            return json;
        }
    }
}

