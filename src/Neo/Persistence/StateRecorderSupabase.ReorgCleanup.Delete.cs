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
            StateRecorderSettings settings,
            StateRecorderSettings.UploadMode effectiveMode)
        {
            // If the canonical hash changed again, do not delete anything (avoid deleting new data).
            if (TryGetCanonicalBlockHash(blockIndex, out var canonical) &&
                !string.Equals(canonical, expectedBlockHash, System.StringComparison.Ordinal))
            {
                return;
            }

            var blockIndexValue = checked((int)blockIndex);

            var backend = ResolveDatabaseBackend(effectiveMode, settings);
            if (backend == DatabaseBackend.None)
                return;

            // Reorg cleanup should not interleave with other uploads (HTTP or direct Postgres).
            // Drain the global semaphore so in-flight uploads complete before we delete, and no new ones start mid-cleanup.
            var acquired = 0;
            try
            {
                for (; acquired < TraceUploadConcurrency; acquired++)
                    await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);

                // Canonical may have changed while we waited for in-flight uploads to finish.
                if (TryGetCanonicalBlockHash(blockIndex, out var canonicalAfterDrain) &&
                    !string.Equals(canonicalAfterDrain, expectedBlockHash, System.StringComparison.Ordinal))
                {
                    return;
                }

                if (backend == DatabaseBackend.Postgres)
                {
                    await TryDeleteBlockDataPostgresAsync(blockIndexValue, settings).ConfigureAwait(false);
                }
                else
                {
                    await DeleteBlockDataRestApiAsync(blockIndexValue, settings).ConfigureAwait(false);
                }
            }
            finally
            {
                for (; acquired > 0; acquired--)
                    TraceUploadSemaphore.Release();
            }
        }
    }
}
