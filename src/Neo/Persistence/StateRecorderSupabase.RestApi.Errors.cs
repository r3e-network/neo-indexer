// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RestApi.Errors.cs file belongs to the neo project and is free
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
        private static bool IsMissingUpsertConstraintError(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return false;

            // PostgREST typically returns:
            // {"code":"42P10",...,"message":"there is no unique or exclusion constraint matching the ON CONFLICT specification"}
            return body.Contains("42P10", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("no unique", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("ON CONFLICT", StringComparison.OrdinalIgnoreCase) &&
                body.Contains("constraint", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUpsertPermissionError(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return false;

            // Common PostgREST errors when UPDATE policies are missing:
            // {"code":"42501",...,"message":"new row violates row-level security policy for table \"storage_reads\""}
            return body.Contains("42501", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("row-level security", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("permission denied", StringComparison.OrdinalIgnoreCase);
        }
    }
}

