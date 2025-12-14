// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.ReorgCleanup.Delete.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task DeleteBlockDataIfCanonicalAsync(
            uint blockIndex,
            string expectedBlockHash,
            StateRecorderSettings settings)
        {
            // If the canonical hash changed again, do not delete anything (avoid deleting new data).
            if (TryGetCanonicalBlockHash(blockIndex, out var canonical) &&
                !string.Equals(canonical, expectedBlockHash, System.StringComparison.Ordinal))
            {
                return;
            }

            var blockIndexValue = checked((int)blockIndex);

            var hasRestApi = settings.UploadEnabled;
            var hasPostgres = !string.IsNullOrWhiteSpace(settings.SupabaseConnectionString);

            // Mirror upload routing:
            // - Postgres mode prefers direct Postgres when configured, otherwise falls back to REST API.
            // - RestApi/Both prefer REST API when configured, otherwise fall back to direct Postgres.
            if (settings.Mode == StateRecorderSettings.UploadMode.Postgres)
            {
                if (hasPostgres)
                {
                    await TryDeleteBlockDataPostgresAsync(blockIndexValue, settings).ConfigureAwait(false);
                    return;
                }

                if (!hasRestApi)
                    return;
            }
            else
            {
                if (hasRestApi)
                {
                    // continue below
                }
                else if (hasPostgres)
                {
                    await TryDeleteBlockDataPostgresAsync(blockIndexValue, settings).ConfigureAwait(false);
                    return;
                }
                else
                {
                    return;
                }
            }

            // Reorg cleanup should not interleave with other HTTP uploads. Drain the global HTTP semaphore
            // so old in-flight trace/uploads complete before we delete, and no new ones start mid-cleanup.
            var acquired = 0;
            try
            {
                for (; acquired < TraceUploadConcurrency; acquired++)
                    await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);

                await DeleteBlockDataRestApiAsync(blockIndexValue, settings).ConfigureAwait(false);
            }
            finally
            {
                for (; acquired > 0; acquired--)
                    TraceUploadSemaphore.Release();
            }
        }
    }
}
