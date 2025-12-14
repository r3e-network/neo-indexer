// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.JsonCsvUpload.Core.cs file belongs to the neo project and is free
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
        private static async Task UploadTextPayloadToStorageAsync(
            StateRecorderSettings settings,
            (string Content, string Path) payload,
            string contentType,
            string formatLower)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put,
                $"{settings.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{settings.SupabaseBucket}/{payload.Path}")
            {
                Content = new StringContent(payload.Content, Encoding.UTF8, contentType)
            };
            request.Headers.TryAddWithoutValidation("apikey", settings.SupabaseApiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SupabaseApiKey);
            request.Headers.TryAddWithoutValidation("x-upsert", "true");

            using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Supabase {formatLower} upload failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        }
    }
}

