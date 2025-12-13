// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.RestApi.Batch.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task UploadTraceBatchRestApiAsync<T>(
            string baseUrl,
            string apiKey,
            string tableName,
            string entityName,
            IReadOnlyList<T> rows,
            int batchSize)
        {
            if (rows.Count == 0)
                return;

            var effectiveBatchSize = batchSize > 0 ? Math.Min(batchSize, MaxTraceBatchSize) : DefaultTraceBatchSize;
            var conflictTarget = GetTraceUpsertConflictTarget(tableName);
            var requestUri = conflictTarget is null
                ? $"{baseUrl}/rest/v1/{tableName}"
                : $"{baseUrl}/rest/v1/{tableName}?on_conflict={conflictTarget}";

            for (var offset = 0; offset < rows.Count; offset += effectiveBatchSize)
            {
                var count = Math.Min(effectiveBatchSize, rows.Count - offset);
                var batch = rows.Skip(offset).Take(count);
                var payload = JsonSerializer.SerializeToUtf8Bytes(batch);
                await SendTraceRequestWithRetryAsync(
                    requestUri,
                    apiKey,
                    payload,
                    entityName).ConfigureAwait(false);
            }
        }
    }
}

