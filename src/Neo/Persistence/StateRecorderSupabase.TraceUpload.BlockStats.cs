// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.BlockStats.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        /// <summary>
        /// Upload aggregated block statistics via the Supabase REST API.
        /// </summary>
        public static Task UploadBlockStatsAsync(BlockStats stats)
        {
            return UploadBlockStatsAsync(stats, expectedBlockHash: string.Empty);
        }

        private static async Task UploadBlockStatsAsync(BlockStats stats, string expectedBlockHash)
        {
            if (stats is null) throw new ArgumentNullException(nameof(stats));

            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled || !IsRestApiMode(settings.Mode))
                return;

            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrWhiteSpace(expectedBlockHash) &&
                    TryGetCanonicalBlockHash(stats.BlockIndex, out var canonical) &&
                    !string.Equals(canonical, expectedBlockHash, StringComparison.Ordinal))
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                        $"Skipping block stats upload for block {stats.BlockIndex}: block hash no longer canonical.");
                    return;
                }

                var backend = ResolveDatabaseBackend(settings.Mode, settings);
                if (backend == DatabaseBackend.None)
                    return;

                if (backend == DatabaseBackend.Postgres)
                {
#if NET9_0_OR_GREATER
                    await UploadBlockStatsPostgresAsync(stats, settings).ConfigureAwait(false);
#endif
                    return;
                }

                var baseUrl = settings.SupabaseUrl.TrimEnd('/');
                var apiKey = settings.SupabaseApiKey;

                var row = new BlockStatsRow(
                    checked((int)stats.BlockIndex),
                    stats.TransactionCount,
                    stats.TotalGasConsumed,
                    stats.OpCodeCount,
                    stats.SyscallCount,
                    stats.ContractCallCount,
                    stats.StorageReadCount,
                    stats.StorageWriteCount,
                    stats.NotificationCount);

                var payload = JsonSerializer.SerializeToUtf8Bytes(new[] { row });
                // Explicit on_conflict for robustness.
                await SendTraceRequestWithRetryAsync($"{baseUrl}/rest/v1/block_stats?on_conflict=block_index", apiKey, payload, "block stats").ConfigureAwait(false);

                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                    $"Block stats upsert successful for block {stats.BlockIndex}");
            }
            finally
            {
                TraceUploadSemaphore.Release();
            }
        }
    }
}
