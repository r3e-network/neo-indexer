// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TransactionResultsUpload.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static Task UploadTransactionResultsAsync(
            uint blockIndex,
            string expectedBlockHash,
            IReadOnlyCollection<ExecutionTraceRecorder> recorders)
        {
            if (recorders is null) throw new ArgumentNullException(nameof(recorders));

            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled || !IsRestApiMode(settings.Mode))
                return Task.CompletedTask;

            return UploadTransactionResultsCoreAsync(
                blockIndex,
                expectedBlockHash,
                recorders,
                settings);
        }
    }
}

