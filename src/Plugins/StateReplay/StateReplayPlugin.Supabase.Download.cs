// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.Supabase.Download.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace StateReplay
{
    public partial class StateReplayPlugin
    {
        private async Task DownloadFromSupabaseAsync(uint blockIndex, string localPath)
        {
            var fileName = $"block-{blockIndex}.bin";
            var url = $"{Settings.Default.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{Settings.Default.SupabaseBucket}/{fileName}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("apikey", Settings.Default.SupabaseApiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.Default.SupabaseApiKey);

            using var response = await HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Download failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var directory = global::System.IO.Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await responseStream.CopyToAsync(fileStream).ConfigureAwait(false);
        }
    }
}
