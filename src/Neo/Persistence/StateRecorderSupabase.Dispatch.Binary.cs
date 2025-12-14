// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Dispatch.Binary.cs file belongs to the neo project and is free
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
        private static void TryQueueBinaryUploads(
            BlockReadRecorder recorder,
            StateRecorderSettings settings,
            StateRecorderSettings.UploadMode effectiveMode)
        {
            if (!IsBinaryMode(effectiveMode) || !settings.UploadEnabled)
                return;

            UploadQueue.TryEnqueueHigh(
                recorder.BlockIndex,
                "binary upload",
                () => ExecuteWithRetryAsync(
                    () => UploadBinaryAsync(recorder, settings),
                    "binary upload",
                    recorder.BlockIndex));

            if (!settings.UploadAuxFormats)
                return;

            UploadQueue.TryEnqueueHigh(
                recorder.BlockIndex,
                "json upload",
                () => ExecuteWithRetryAsync(
                    () => UploadJsonAsync(recorder, settings),
                    "json upload",
                    recorder.BlockIndex));

            UploadQueue.TryEnqueueHigh(
                recorder.BlockIndex,
                "csv upload",
                () => ExecuteWithRetryAsync(
                    () => UploadCsvAsync(recorder, settings),
                    "csv upload",
                    recorder.BlockIndex));
        }
    }
}

