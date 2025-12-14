// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RestApiUpload.StorageReads.Delete.cs file belongs to the neo project and is free
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
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task DeleteStorageReadsRestApiAsync(string baseUrl, string apiKey, int blockIndex)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete,
                $"{baseUrl}/rest/v1/storage_reads?block_index=eq.{blockIndex}");
            AddRestApiHeaders(request, apiKey);

            using var response = await HttpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            // 404 is OK (no records to delete), other errors should throw
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"REST API delete failed: {(int)response.StatusCode} {body}");
            }
        }
    }
}

