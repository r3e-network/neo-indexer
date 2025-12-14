// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Traces.RuntimeLogs.cs file belongs to the neo project and is free
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
        private static Task UpsertRuntimeLogTracesPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            IReadOnlyList<RuntimeLogTraceRow> rows,
            int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "log_order",
                "contract_hash",
                "message"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.LogOrder,
                r.ContractHash,
                r.Message
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "runtime_logs",
                columns,
                "block_index, tx_hash, log_order",
                "contract_hash = EXCLUDED.contract_hash, message = EXCLUDED.message",
                values,
                batchSize);
        }
#endif
    }
}

