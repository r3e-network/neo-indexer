// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.JsonCsvUpload.Json.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task UploadJsonAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                var entries = GetOrderedEntries(recorder);
                var jsonPayload = BuildJsonPayload(recorder, entries);

                await UploadTextPayloadToStorageAsync(
                    settings,
                    jsonPayload,
                    contentType: "application/json",
                    formatLower: "json").ConfigureAwait(false);

                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                    $"JSON upload successful for block {recorder.BlockIndex}: {entries.Length} entries");
            }
            finally
            {
                TraceUploadSemaphore.Release();
            }
        }
    }
}

