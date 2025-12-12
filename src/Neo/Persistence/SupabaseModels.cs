// Copyright (C) 2015-2025 The Neo Project.
//
// SupabaseModels.cs file belongs to the neo project and is free
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
    /// DTO for blocks table in Supabase PostgreSQL.
    /// </summary>
    internal sealed record BlockRecord(
        int BlockIndex,
        string Hash,
        long TimestampMs,
        int TransactionCount,
        int ReadKeyCount);

    /// <summary>
    /// DTO for contracts table in Supabase PostgreSQL.
    /// </summary>
    internal sealed record ContractRecord(
        int ContractId,
        string ContractHash,
        string? ManifestName);

    /// <summary>
    /// DTO for storage_reads table in Supabase PostgreSQL.
    /// </summary>
    internal sealed record StorageReadRecord(
        int BlockIndex,
        int? ContractId,
        string KeyBase64,
        string ValueBase64,
        int ReadOrder,
        string? TxHash,
        string? Source);

    /// <summary>
    /// DTO for opcode_traces table rows.
    /// </summary>
    internal sealed record OpCodeTraceRow(
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
    internal sealed record SyscallTraceRow(
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
    internal sealed record ContractCallTraceRow(
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
    internal sealed record StorageWriteTraceRow(
        [property: JsonPropertyName("block_index")] int BlockIndex,
        [property: JsonPropertyName("tx_hash")] string TxHash,
        [property: JsonPropertyName("write_order")] int WriteOrder,
        [property: JsonPropertyName("contract_id")] int? ContractId,
        [property: JsonPropertyName("contract_hash")] string ContractHash,
        [property: JsonPropertyName("key_base64")] string KeyBase64,
        [property: JsonPropertyName("old_value_base64")] string? OldValueBase64,
        [property: JsonPropertyName("new_value_base64")] string NewValueBase64);

    /// <summary>
    /// DTO for notifications trace table rows.
    /// </summary>
    internal sealed record NotificationTraceRow(
        [property: JsonPropertyName("block_index")] int BlockIndex,
        [property: JsonPropertyName("tx_hash")] string TxHash,
        [property: JsonPropertyName("notification_order")] int NotificationOrder,
        [property: JsonPropertyName("contract_hash")] string ContractHash,
        [property: JsonPropertyName("event_name")] string EventName,
        [property: JsonPropertyName("state_json")] JsonElement? StateJson);

    /// <summary>
    /// DTO for block_stats table rows.
    /// </summary>
    internal sealed record BlockStatsRow(
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
