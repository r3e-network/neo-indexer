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
            bool allowBinaryUploads,
            bool allowRestApiUploads,
            int storageReadCount)
        {
            if (!allowBinaryUploads && !allowRestApiUploads)
                return;

            if (storageReadCount > 0)
            {
                var effectiveReadMode =
                    allowBinaryUploads && allowRestApiUploads
                        ? StateRecorderSettings.UploadMode.Both
                        : allowBinaryUploads
                            ? StateRecorderSettings.UploadMode.Binary
                            : StateRecorderSettings.UploadMode.RestApi;

                StateRecorderSupabase.TryUpload(readRecorder, effectiveReadMode);
            }
            else if (allowRestApiUploads)
            {
                // Still upsert the block row (read_key_count=0) so the frontend can
                // search blocks even when no storage keys were touched. Avoid binary
                // snapshot uploads for empty read sets to prevent file explosion.
                StateRecorderSupabase.TryUpload(readRecorder, StateRecorderSettings.UploadMode.RestApi);
            }
        }
    }
}

