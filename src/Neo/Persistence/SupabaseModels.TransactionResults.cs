// Copyright (C) 2015-2025 The Neo Project.
//
// SupabaseModels.TransactionResults.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neo.Persistence
{
    /// <summary>
    /// DTO for transaction_results table rows.
    /// </summary>
    internal readonly record struct TransactionResultRow(
        [property: JsonPropertyName("block_index")] int BlockIndex,
        [property: JsonPropertyName("tx_hash")] string TxHash,
        [property: JsonPropertyName("vm_state")] int VmState,
        [property: JsonPropertyName("vm_state_name")] string VmStateName,
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("gas_consumed")] long GasConsumed,
        [property: JsonPropertyName("fault_exception")] string? FaultException,
        [property: JsonPropertyName("result_stack_json")] JsonElement? ResultStackJson,
        [property: JsonPropertyName("opcode_count")] int OpCodeCount,
        [property: JsonPropertyName("syscall_count")] int SyscallCount,
        [property: JsonPropertyName("contract_call_count")] int ContractCallCount,
        [property: JsonPropertyName("storage_write_count")] int StorageWriteCount,
        [property: JsonPropertyName("notification_count")] int NotificationCount);
}

