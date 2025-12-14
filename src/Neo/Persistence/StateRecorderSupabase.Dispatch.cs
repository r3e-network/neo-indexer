// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Dispatch.cs file belongs to the neo project and is free
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
    /// <summary>
    /// Handles uploading recorded block state to Supabase.
    /// Supports binary uploads to Storage bucket and/or REST API inserts.
    /// Robust design: Supports re-sync by automatically replacing existing block data.
    /// </summary>
    public static partial class StateRecorderSupabase
    {
        /// <summary>
        /// Trigger upload of recorded block state based on configured mode.
        /// Runs asynchronously on background thread pool.
        /// </summary>
        public static void TryUpload(BlockReadRecorder recorder, StateRecorderSettings.UploadMode? modeOverride = null)
        {
            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled) return;

            var effectiveMode = modeOverride ?? settings.Mode;
            var blockHash = recorder.BlockHash.ToString();

            // Track the latest canonical block hash per height (in-process) so queued uploads
            // can avoid writing stale data during short tip reorgs.
            var (hadPrevious, previousHash) = UpdateCanonicalBlockHash(recorder.BlockIndex, blockHash);

            // If the same height is observed again with a different hash (tip reorg), and trimming is enabled,
            // delete per-block rows (reads + traces) before re-uploading to avoid orphan data.
            if (settings.TrimStaleTraceRows &&
                IsRestApiMode(effectiveMode) &&
                hadPrevious &&
                !string.IsNullOrWhiteSpace(previousHash) &&
                !string.Equals(previousHash, blockHash, System.StringComparison.Ordinal) &&
                (settings.UploadEnabled || !string.IsNullOrWhiteSpace(settings.SupabaseConnectionString)))
            {
                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                    $"Detected block hash replacement at height {recorder.BlockIndex} (reorg). Scheduling per-block cleanup before re-upload.");
                TryQueueReorgCleanup(recorder.BlockIndex, blockHash, settings);
            }

            TryQueueBinaryUploads(recorder, settings, effectiveMode, blockHash);

            TryQueueDatabaseUploads(recorder, settings, effectiveMode, blockHash);
        }

        /// <summary>
        /// Legacy method for backward compatibility.
        /// </summary>
        internal static void TryUpload(BlockReadRecorder recorder, string format)
        {
            TryUpload(recorder);
        }
    }
}
