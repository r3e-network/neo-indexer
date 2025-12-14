// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.BinaryUpload.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        #region Binary Upload

        private static async Task UploadBinaryAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                var expectedBlockHash = recorder.BlockHash.ToString();
                if (TryGetCanonicalBlockHash(recorder.BlockIndex, out var canonical) &&
                    !string.Equals(canonical, expectedBlockHash, System.StringComparison.Ordinal))
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                        $"Skipping binary upload for block {recorder.BlockIndex}: block hash no longer canonical.");
                    return;
                }

                var orderedEntries = GetOrderedEntries(recorder);
                var payload = BuildBinaryPayload(recorder, orderedEntries);

                // Use PUT for upsert behavior (overwrite if exists)
                using var request = new HttpRequestMessage(HttpMethod.Put,
                    $"{settings.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{settings.SupabaseBucket}/{payload.Path}")
                {
                    Content = new ByteArrayContent(payload.Buffer)
                };
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                request.Headers.TryAddWithoutValidation("apikey", settings.SupabaseApiKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SupabaseApiKey);
                request.Headers.TryAddWithoutValidation("x-upsert", "true");

                using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Supabase binary upload failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                    $"Binary upload successful for block {recorder.BlockIndex}: {orderedEntries.Length} entries, {payload.Buffer.Length} bytes");
            }
            finally
            {
                TraceUploadSemaphore.Release();
            }
        }

        #endregion
    }
}
