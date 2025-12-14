// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Settings.Trace.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static int GetTraceUploadBatchSize()
        {
            var raw = Environment.GetEnvironmentVariable(TraceBatchSizeEnvVar);
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
            {
                return Math.Min(parsed, MaxTraceBatchSize);
            }
            return DefaultTraceBatchSize;
        }

        private static int GetTraceUploadConcurrency()
        {
            var raw = Environment.GetEnvironmentVariable(TraceUploadConcurrencyEnvVar);
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
                return parsed;
            return 4;
        }

        private static int GetLowPriorityTraceLaneConcurrency()
        {
            // Reserve at least one global upload slot for high-priority uploads.
            // When concurrency is 1, there is nothing to reserve; traces must use the only slot.
            return TraceUploadConcurrency <= 1 ? 1 : TraceUploadConcurrency - 1;
        }
    }
}

