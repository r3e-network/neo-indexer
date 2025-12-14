// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexerPlugin.Handlers.Committed.StorageReads.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class BlockStateIndexerPlugin
    {
        private static void TryUploadStorageReads(
            BlockReadRecorder readRecorder,
            StateRecorderSettings recorderSettings,
            bool allowBinaryUploads,
            bool allowDatabaseUploads,
            int storageReadCount)
        {
            if (!allowBinaryUploads && !allowDatabaseUploads)
                return;

            var allowBinaryAndDatabase = allowBinaryUploads && allowDatabaseUploads;
            var databaseOnlyMode = recorderSettings.Mode == StateRecorderSettings.UploadMode.Postgres
                ? StateRecorderSettings.UploadMode.Postgres
                : StateRecorderSettings.UploadMode.RestApi;

            if (storageReadCount > 0)
            {
                var effectiveReadMode = allowBinaryAndDatabase
                    ? StateRecorderSettings.UploadMode.Both
                    : allowBinaryUploads
                        ? StateRecorderSettings.UploadMode.Binary
                        : databaseOnlyMode;

                StateRecorderSupabase.TryUpload(readRecorder, effectiveReadMode);
            }
            else if (allowDatabaseUploads)
            {
                // Still upsert the block row (read_key_count=0) so the frontend can
                // search blocks even when no storage keys were touched. Avoid binary
                // snapshot uploads for empty read sets to prevent file explosion.
                StateRecorderSupabase.TryUpload(readRecorder, databaseOnlyMode);
            }
        }
    }
}
