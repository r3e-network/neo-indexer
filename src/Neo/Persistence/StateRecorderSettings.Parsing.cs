// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSettings.Parsing.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.SmartContract;
using System;
using System.Globalization;

namespace Neo.Persistence
{
    public sealed partial class StateRecorderSettings
    {
        private static UploadMode ParseUploadMode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return UploadMode.Binary;

            return Enum.TryParse(value, true, out UploadMode mode) ? mode : UploadMode.Binary;
        }

        private static ExecutionTraceLevel ParseTraceLevel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ExecutionTraceLevel.All;

            // Support comma-separated lists of flags (e.g., "Syscalls,OpCodes")
            var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                ExecutionTraceLevel combined = ExecutionTraceLevel.None;
                foreach (var part in parts)
                {
                    if (Enum.TryParse(part.Trim(), true, out ExecutionTraceLevel parsed))
                        combined |= parsed;
                }
                return combined == ExecutionTraceLevel.None ? ExecutionTraceLevel.All : combined;
            }

            return Enum.TryParse(value, true, out ExecutionTraceLevel mode) ? mode : ExecutionTraceLevel.All;
        }

        private static bool GetEnvBool(string name)
        {
            var value = Environment.GetEnvironmentVariable($"{Prefix}{name}");
            return value != null && bool.TryParse(value, out var result) ? result : false;
        }

        private static int GetEnvInt(string name)
        {
            var value = Environment.GetEnvironmentVariable($"{Prefix}{name}");
            if (value is null) return 0;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? Math.Max(0, result) : 0;
        }
    }
}

