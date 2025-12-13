// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.JsonCsvUpload.cs file belongs to the neo project and is free
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
        #region JSON/CSV Upload

        private static async Task UploadJsonAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                var entries = GetOrderedEntries(recorder);
                var jsonPayload = BuildJsonPayload(recorder, entries);

                using var request = new HttpRequestMessage(HttpMethod.Put,
                    $"{settings.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{settings.SupabaseBucket}/{jsonPayload.Path}")
                {
                    Content = new StringContent(jsonPayload.Content, Encoding.UTF8, "application/json")
                };
                request.Headers.TryAddWithoutValidation("apikey", settings.SupabaseApiKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SupabaseApiKey);
                request.Headers.TryAddWithoutValidation("x-upsert", "true");

                using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Supabase json upload failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                    $"JSON upload successful for block {recorder.BlockIndex}: {entries.Length} entries");
            }
            finally
            {
                TraceUploadSemaphore.Release();
            }
        }

        private static async Task UploadCsvAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            await TraceUploadSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                var entries = GetOrderedEntries(recorder);
                var csvPayload = BuildCsvPayload(recorder, entries);

                using var request = new HttpRequestMessage(HttpMethod.Put,
                    $"{settings.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{settings.SupabaseBucket}/{csvPayload.Path}")
                {
                    Content = new StringContent(csvPayload.Content, Encoding.UTF8, "text/csv")
                };
                request.Headers.TryAddWithoutValidation("apikey", settings.SupabaseApiKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SupabaseApiKey);
                request.Headers.TryAddWithoutValidation("x-upsert", "true");

                using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Supabase csv upload failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                    $"CSV upload successful for block {recorder.BlockIndex}: {entries.Length} entries");
            }
            finally
            {
                TraceUploadSemaphore.Release();
            }
        }

        #endregion

    }
}
