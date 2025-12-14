// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Traces.StorageWrites.cs file belongs to the neo project and is free
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
        private static Task UpsertStorageWriteTracesPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            IReadOnlyList<StorageWriteTraceRow> rows,
            int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "write_order",
                "contract_id",
                "contract_hash",
                "is_delete",
                "key_base64",
                "old_value_base64",
                "new_value_base64"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.WriteOrder,
                r.ContractId,
                r.ContractHash,
                r.IsDelete,
                r.KeyBase64,
                r.OldValueBase64,
                r.NewValueBase64
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "storage_writes",
                columns,
                "block_index, tx_hash, write_order",
                "contract_id = EXCLUDED.contract_id, contract_hash = EXCLUDED.contract_hash, is_delete = EXCLUDED.is_delete, key_base64 = EXCLUDED.key_base64, old_value_base64 = EXCLUDED.old_value_base64, new_value_base64 = EXCLUDED.new_value_base64",
                values,
                batchSize);
        }
#endif
    }
}
