// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Supabase.Settings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;
using System;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private static StateRecorderSettings EnsureSupabaseTraceSettings()
        {
            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled)
                throw new RpcException(RpcError.InternalServerError.WithData("state recorder not enabled"));
            if (string.IsNullOrWhiteSpace(settings.SupabaseUrl) || string.IsNullOrWhiteSpace(settings.SupabaseApiKey))
                throw new RpcException(RpcError.InternalServerError.WithData("supabase connection not configured"));

            var overrideKey = Environment.GetEnvironmentVariable(RpcTracesSupabaseKeyEnvVar);
            if (!string.IsNullOrWhiteSpace(overrideKey) && !string.Equals(overrideKey, settings.SupabaseApiKey, StringComparison.Ordinal))
            {
                settings = new StateRecorderSettings
                {
                    Enabled = settings.Enabled,
                    SupabaseUrl = settings.SupabaseUrl,
                    SupabaseApiKey = overrideKey,
                    SupabaseBucket = settings.SupabaseBucket,
                    SupabaseConnectionString = settings.SupabaseConnectionString,
                    Mode = settings.Mode,
                    TraceLevel = settings.TraceLevel,
                    TrimStaleTraceRows = settings.TrimStaleTraceRows,
                    UploadAuxFormats = settings.UploadAuxFormats,
                    MaxStorageReadsPerBlock = settings.MaxStorageReadsPerBlock
                };
            }
            return settings;
        }
    }
}

