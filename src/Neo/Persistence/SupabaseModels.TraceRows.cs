// Copyright (C) 2015-2025 The Neo Project.
//
// SupabaseModels.TraceRows.cs file belongs to the neo project and is free
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
    /// DTO for opcode_traces table rows.
    /// </summary>
    internal readonly record struct OpCodeTraceRow(
        [property: JsonPropertyName("block_index")] int BlockIndex,
        [property: JsonPropertyName("tx_hash")] string TxHash,
        [property: JsonPropertyName("trace_order")] int TraceOrder,
        [property: JsonPropertyName("contract_hash")] string ContractHash,
        [property: JsonPropertyName("instruction_pointer")] int InstructionPointer,
        [property: JsonPropertyName("opcode")] int OpCode,
        [property: JsonPropertyName("opcode_name")] string OpCodeName,
        [property: JsonPropertyName("operand_base64")] string? OperandBase64,
        [property: JsonPropertyName("gas_consumed")] long GasConsumed,
        [property: JsonPropertyName("stack_depth")] int? StackDepth);

    /// <summary>
    /// DTO for syscall_traces table rows.
    /// </summary>
    internal readonly record struct SyscallTraceRow(
        [property: JsonPropertyName("block_index")] int BlockIndex,
        [property: JsonPropertyName("tx_hash")] string TxHash,
        [property: JsonPropertyName("trace_order")] int TraceOrder,
        [property: JsonPropertyName("contract_hash")] string ContractHash,
        [property: JsonPropertyName("syscall_hash")] string SyscallHash,
        [property: JsonPropertyName("syscall_name")] string SyscallName,
        [property: JsonPropertyName("gas_cost")] long GasCost);

    /// <summary>
    /// DTO for contract_calls table rows.
    /// </summary>
    internal readonly record struct ContractCallTraceRow(
        [property: JsonPropertyName("block_index")] int BlockIndex,
        [property: JsonPropertyName("tx_hash")] string TxHash,
        [property: JsonPropertyName("trace_order")] int TraceOrder,
        [property: JsonPropertyName("caller_hash")] string? CallerHash,
        [property: JsonPropertyName("callee_hash")] string CalleeHash,
        [property: JsonPropertyName("method_name")] string? MethodName,
        [property: JsonPropertyName("call_depth")] int CallDepth,
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("gas_consumed")] long? GasConsumed);

    /// <summary>
    /// DTO for storage_writes trace table rows.
    /// </summary>
    internal readonly record struct StorageWriteTraceRow(
        [property: JsonPropertyName("block_index")] int BlockIndex,
        [property: JsonPropertyName("tx_hash")] string TxHash,
        [property: JsonPropertyName("write_order")] int WriteOrder,
        [property: JsonPropertyName("contract_id")] int? ContractId,
        [property: JsonPropertyName("contract_hash")] string ContractHash,
        [property: JsonPropertyName("is_delete")] bool IsDelete,
        [property: JsonPropertyName("key_base64")] string KeyBase64,
        [property: JsonPropertyName("old_value_base64")] string? OldValueBase64,
        [property: JsonPropertyName("new_value_base64")] string NewValueBase64);

    /// <summary>
    /// DTO for notifications trace table rows.
    /// </summary>
    internal readonly record struct NotificationTraceRow(
        [property: JsonPropertyName("block_index")] int BlockIndex,
        [property: JsonPropertyName("tx_hash")] string TxHash,
        [property: JsonPropertyName("notification_order")] int NotificationOrder,
        [property: JsonPropertyName("contract_hash")] string ContractHash,
        [property: JsonPropertyName("event_name")] string EventName,
        [property: JsonPropertyName("state_json")] JsonElement? StateJson);

    /// <summary>
    /// DTO for runtime_logs trace table rows.
    /// </summary>
    internal readonly record struct RuntimeLogTraceRow(
        [property: JsonPropertyName("block_index")] int BlockIndex,
        [property: JsonPropertyName("tx_hash")] string TxHash,
        [property: JsonPropertyName("log_order")] int LogOrder,
        [property: JsonPropertyName("contract_hash")] string ContractHash,
        [property: JsonPropertyName("message")] string Message);
}
