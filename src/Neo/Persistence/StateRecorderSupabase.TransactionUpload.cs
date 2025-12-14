// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TransactionUpload.cs file belongs to the neo project and is free
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
        private static Task UploadTransactionAsync(
            uint blockIndex,
            string expectedBlockHash,
            string txHash,
            ExecutionTraceRecorder recorder,
            bool uploadTraces)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));

            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled || !IsRestApiMode(settings.Mode))
                return Task.CompletedTask;

            return UploadTransactionCoreAsync(
                blockIndex,
                txHash,
                recorder,
                settings,
                expectedBlockHash,
                uploadTraces);
        }
    }
}

