// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.RestApi.Transport.Helpers.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static HttpRequestMessage CreateTracePostRequest(string requestUri, string apiKey, byte[] jsonPayload)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new ByteArrayContent(jsonPayload)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            AddRestApiHeaders(request, apiKey);
            request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
            return request;
        }

        private static bool IsRetryableTraceUploadStatus(HttpStatusCode statusCode)
        {
            return statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;
        }
    }
}

