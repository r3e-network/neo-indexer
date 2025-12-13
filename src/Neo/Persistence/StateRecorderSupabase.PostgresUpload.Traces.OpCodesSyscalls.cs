// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Traces.OpCodesSyscalls.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
#endif

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
#if NET9_0_OR_GREATER
        private static Task UpsertOpCodeTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<OpCodeTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "trace_order",
                "contract_hash",
                "instruction_pointer",
                "opcode",
                "opcode_name",
                "operand_base64",
                "gas_consumed",
                "stack_depth"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.TraceOrder,
                r.ContractHash,
                r.InstructionPointer,
                r.OpCode,
                r.OpCodeName,
                r.OperandBase64,
                r.GasConsumed,
                r.StackDepth
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "opcode_traces",
                columns,
                "block_index, tx_hash, trace_order",
                "contract_hash = EXCLUDED.contract_hash, instruction_pointer = EXCLUDED.instruction_pointer, opcode = EXCLUDED.opcode, opcode_name = EXCLUDED.opcode_name, operand_base64 = EXCLUDED.operand_base64, gas_consumed = EXCLUDED.gas_consumed, stack_depth = EXCLUDED.stack_depth",
                values,
                batchSize);
        }

        private static Task UpsertSyscallTracesPostgresAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<SyscallTraceRow> rows, int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "trace_order",
                "contract_hash",
                "syscall_hash",
                "syscall_name",
                "gas_cost"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.TraceOrder,
                r.ContractHash,
                r.SyscallHash,
                r.SyscallName,
                r.GasCost
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "syscall_traces",
                columns,
                "block_index, tx_hash, trace_order",
                "contract_hash = EXCLUDED.contract_hash, syscall_hash = EXCLUDED.syscall_hash, syscall_name = EXCLUDED.syscall_name, gas_cost = EXCLUDED.gas_cost",
                values,
                batchSize);
        }
#endif
    }
}

