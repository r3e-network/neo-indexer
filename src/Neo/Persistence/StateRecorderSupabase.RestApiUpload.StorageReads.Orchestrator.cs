// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RestApiUpload.StorageReads.Orchestrator.cs file belongs to the neo project and is free
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
        private static async Task UpsertStorageReadsRestApiAsync(
            int blockIndex,
            string baseUrl,
            string apiKey,
            List<StorageReadRecord> storageReads)
        {
            var upserted = await TryUpsertStorageReadsRestApiAsync(baseUrl, apiKey, storageReads).ConfigureAwait(false);
            if (upserted)
                return;

            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                $"Block {blockIndex}: storage_reads upsert not available (missing unique index). Falling back to delete+insert.");

            await DeleteStorageReadsRestApiAsync(baseUrl, apiKey, blockIndex).ConfigureAwait(false);
            await InsertStorageReadsRestApiAsync(baseUrl, apiKey, storageReads).ConfigureAwait(false);
        }
    }
}

