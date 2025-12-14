// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Settings.cs file belongs to the neo project and is free
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
        private static int GetUploadQueueWorkers()
        {
            var raw = Environment.GetEnvironmentVariable(UploadQueueWorkersEnvVar);
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
                return parsed;
            return TraceUploadConcurrency;
        }

        private static int GetPositiveEnvIntOrDefault(string envVar, int defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
                return parsed;
            return defaultValue;
        }
    }
}
