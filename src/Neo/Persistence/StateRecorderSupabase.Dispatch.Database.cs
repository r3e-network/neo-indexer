// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Dispatch.Database.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static void TryQueueDatabaseUploads(
            BlockReadRecorder recorder,
            StateRecorderSettings settings,
            StateRecorderSettings.UploadMode effectiveMode,
            string blockHash)
        {
            if (!IsRestApiMode(effectiveMode))
                return;

            var backend = ResolveDatabaseBackend(effectiveMode, settings);
            switch (backend)
            {
                case DatabaseBackend.Postgres:
                    UploadQueue.TryEnqueueHigh(
                        recorder.BlockIndex,
                        "PostgreSQL upsert",
                        () => ExecuteWithRetryAsync(
                            () => ExecuteIfCanonicalAsync(
                                recorder.BlockIndex,
                                blockHash,
                                "PostgreSQL upsert",
                                () => UploadPostgresAsync(recorder, settings)),
                            "PostgreSQL upsert",
                            recorder.BlockIndex));
                    break;

                case DatabaseBackend.RestApi:
                    UploadQueue.TryEnqueueHigh(
                        recorder.BlockIndex,
                        "REST API upsert",
                        () => ExecuteWithRetryAsync(
                            () => ExecuteIfCanonicalAsync(
                                recorder.BlockIndex,
                                blockHash,
                                "REST API upsert",
                                () => UploadRestApiAsync(recorder, settings)),
                            "REST API upsert",
                            recorder.BlockIndex));
                    break;
            }
        }
    }
}
