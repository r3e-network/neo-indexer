// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.BlockState.StorageReads.Upsert.cs file belongs to the neo project and is free
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
        private static Task UpsertStorageReadsPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            List<StorageReadRecord> reads)
        {
            var columns = new[]
            {
                "block_index",
                "contract_id",
                "key_base64",
                "value_base64",
                "read_order",
                "tx_hash",
                "source"
            };

            var values = reads.Select(r => new object?[]
            {
                r.BlockIndex,
                r.ContractId,
                r.KeyBase64,
                r.ValueBase64,
                r.ReadOrder,
                r.TxHash,
                r.Source
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "storage_reads",
                columns,
                "block_index, contract_id, key_base64",
                "value_base64 = EXCLUDED.value_base64, read_order = EXCLUDED.read_order, tx_hash = EXCLUDED.tx_hash, source = EXCLUDED.source",
                values,
                StorageReadBatchSize);
        }
#endif
    }
}

