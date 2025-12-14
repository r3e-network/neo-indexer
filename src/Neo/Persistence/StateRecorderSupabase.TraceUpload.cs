// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        /// <summary>
        /// Uploads the execution traces captured for a transaction.
        /// </summary>
        public static Task UploadBlockTraceAsync(uint blockIndex, ExecutionTraceRecorder recorder)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));

            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled || !IsRestApiMode(settings.Mode))
                return Task.CompletedTask;

            if (!recorder.HasTraces)
                return Task.CompletedTask;

            var txHash = recorder.TxHash?.ToString();
            if (string.IsNullOrEmpty(txHash))
            {
                throw new InvalidOperationException(
                    "ExecutionTraceRecorder must include a transaction hash before uploading traces.");
            }

            return UploadBlockTraceCoreAsync(blockIndex, txHash, recorder, settings, expectedBlockHash: string.Empty);
        }

        private static Task UploadBlockTraceAsync(uint blockIndex, string expectedBlockHash, ExecutionTraceRecorder recorder)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));

            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled || !IsRestApiMode(settings.Mode))
                return Task.CompletedTask;

            if (!recorder.HasTraces)
                return Task.CompletedTask;

            var txHash = recorder.TxHash?.ToString();
            if (string.IsNullOrEmpty(txHash))
            {
                throw new InvalidOperationException(
                    "ExecutionTraceRecorder must include a transaction hash before uploading traces.");
            }

            return UploadBlockTraceCoreAsync(blockIndex, txHash, recorder, settings, expectedBlockHash);
        }
    }
}
