// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.RestApi.Orchestrator.Tasks.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static void MaybeAddTraceUploadTask<T>(
            List<Task> uploadTasks,
            string baseUrl,
            string apiKey,
            string tableName,
            string entityName,
            IReadOnlyList<T> rows,
            int batchSize,
            int blockIndex,
            string txHash,
            bool trimStaleTraceRows)
        {
            if (!trimStaleTraceRows && rows.Count == 0)
                return;

            uploadTasks.Add(UploadAndMaybeTrimTraceTableRestApiAsync(
                baseUrl,
                apiKey,
                tableName,
                entityName,
                rows,
                batchSize,
                blockIndex,
                txHash,
                trimStaleTraceRows));
        }
    }
}

