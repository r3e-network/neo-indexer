// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Core.DatabaseBackend.cs file belongs to the neo project and is free
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
        private enum DatabaseBackend
        {
            None = 0,
            RestApi = 1,
            Postgres = 2
        }

        private static DatabaseBackend ResolveDatabaseBackend(
            StateRecorderSettings.UploadMode mode,
            StateRecorderSettings settings)
        {
            var hasRestApi = settings.UploadEnabled;
            var hasPostgres = !string.IsNullOrWhiteSpace(settings.SupabaseConnectionString);
            return ResolveDatabaseBackend(mode, hasRestApi, hasPostgres);
        }

        private static DatabaseBackend ResolveDatabaseBackend(
            StateRecorderSettings.UploadMode mode,
            bool hasRestApi,
            bool hasPostgres)
        {
            // Routing rules:
            // - Postgres mode prefers direct Postgres when configured, otherwise falls back to REST API.
            // - RestApi/Both modes prefer REST API when configured, otherwise fall back to direct Postgres.
            // - Binary mode does not use a database backend.

            if (mode == StateRecorderSettings.UploadMode.Postgres)
            {
                if (hasPostgres) return DatabaseBackend.Postgres;
                if (hasRestApi) return DatabaseBackend.RestApi;
                return DatabaseBackend.None;
            }

            if (mode is StateRecorderSettings.UploadMode.RestApi or StateRecorderSettings.UploadMode.Both)
            {
                if (hasRestApi) return DatabaseBackend.RestApi;
                if (hasPostgres) return DatabaseBackend.Postgres;
                return DatabaseBackend.None;
            }

            return DatabaseBackend.None;
        }
    }
}

