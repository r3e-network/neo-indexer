// Copyright (C) 2015-2025 The Neo Project.
//
// SupabaseModels.BlockStats.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Text.Json.Serialization;

namespace Neo.Persistence
{
    /// <summary>
    /// DTO for block_stats table rows.
    /// </summary>
    internal readonly record struct BlockStatsRow(
        [property: JsonPropertyName("block_index")] int BlockIndex,
        [property: JsonPropertyName("tx_count")] int TransactionCount,
        [property: JsonPropertyName("total_gas_consumed")] long TotalGasConsumed,
        [property: JsonPropertyName("opcode_count")] int OpCodeCount,
        [property: JsonPropertyName("syscall_count")] int SyscallCount,
        [property: JsonPropertyName("contract_call_count")] int ContractCallCount,
        [property: JsonPropertyName("storage_read_count")] int StorageReadCount,
        [property: JsonPropertyName("storage_write_count")] int StorageWriteCount,
        [property: JsonPropertyName("notification_count")] int NotificationCount);
}

