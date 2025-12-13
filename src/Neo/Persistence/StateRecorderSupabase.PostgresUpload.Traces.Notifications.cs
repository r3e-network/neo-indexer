// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.Traces.Notifications.cs file belongs to the neo project and is free
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
        private static Task UpsertNotificationTracesPostgresAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            IReadOnlyList<NotificationTraceRow> rows,
            int batchSize)
        {
            var columns = new[]
            {
                "block_index",
                "tx_hash",
                "notification_order",
                "contract_hash",
                "event_name",
                "state_json"
            };

            var values = rows.Select(r => new object?[]
            {
                r.BlockIndex,
                r.TxHash,
                r.NotificationOrder,
                r.ContractHash,
                r.EventName,
                r.StateJson.HasValue ? r.StateJson.Value.GetRawText() : null
            }).ToList();

            return UpsertRowsPostgresAsync(
                connection,
                transaction,
                "notifications",
                columns,
                "block_index, tx_hash, notification_order",
                "contract_hash = EXCLUDED.contract_hash, event_name = EXCLUDED.event_name, state_json = EXCLUDED.state_json",
                values,
                batchSize);
        }
#endif
    }
}

