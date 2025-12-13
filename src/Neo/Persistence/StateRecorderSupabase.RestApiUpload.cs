// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RestApiUpload.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Extensions;
using Neo.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
using NpgsqlTypes;
#endif


namespace Neo.Persistence
{
	public static partial class StateRecorderSupabase
	{
        #region REST API Upload

        /// <summary>
        /// Upload block state using Supabase PostgREST API (HTTPS).
        /// This bypasses direct PostgreSQL connection, useful when IPv6 is blocked or pooler is unavailable.
        /// </summary>
	        private static async Task UploadRestApiAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
	        {
	            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
	            try
	            {
	                var entries = GetOrderedEntries(recorder);
	                var blockRecord = BuildBlockRecord(recorder, entries);
	                var storageReads = BuildStorageReadRecords(recorder, entries);
	                var contracts = BuildContractRecords(entries);

	                var baseUrl = settings.SupabaseUrl.TrimEnd('/');
	                var apiKey = settings.SupabaseApiKey;

	                // Step 1: Upsert block record
	                await UpsertBlockRestApiAsync(baseUrl, apiKey, blockRecord).ConfigureAwait(false);

	                // Step 2: Upsert contracts (if any new ones)
	                if (contracts.Count > 0)
	                {
	                    await UpsertContractsRestApiAsync(baseUrl, apiKey, contracts).ConfigureAwait(false);
	                }

	                // Step 3: Upsert storage reads in batches (preferred, requires migration 012 unique index).
	                // Falls back to delete+insert for older schemas.
	                if (storageReads.Count > 0)
	                {
	                    var upserted = await TryUpsertStorageReadsRestApiAsync(baseUrl, apiKey, storageReads).ConfigureAwait(false);
	                    if (!upserted)
	                    {
	                        Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
	                            $"Block {recorder.BlockIndex}: storage_reads upsert not available (missing unique index). Falling back to delete+insert.");

	                        await DeleteStorageReadsRestApiAsync(baseUrl, apiKey, blockRecord.BlockIndex).ConfigureAwait(false);
	                        await InsertStorageReadsRestApiAsync(baseUrl, apiKey, storageReads).ConfigureAwait(false);
	                    }
	                }

	                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
	                    $"REST API upsert successful for block {recorder.BlockIndex}: {storageReads.Count} reads, {contracts.Count} new contracts");
	            }
	            finally
	            {
	                TraceUploadSemaphore.Release();
	            }
	        }

	        #endregion
	}
}
