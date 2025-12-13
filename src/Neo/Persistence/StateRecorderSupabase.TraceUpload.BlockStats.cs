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
        public static async Task UploadBlockStatsAsync(BlockStats stats)
        {
            if (stats is null) throw new ArgumentNullException(nameof(stats));

            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled || !IsRestApiMode(settings.Mode))
                return;

            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                var useDirectPostgres = settings.Mode == StateRecorderSettings.UploadMode.Postgres || !settings.UploadEnabled;
                if (useDirectPostgres)
                {
                    if (string.IsNullOrWhiteSpace(settings.SupabaseConnectionString))
                        return;

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

