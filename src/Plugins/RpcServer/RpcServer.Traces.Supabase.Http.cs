// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Supabase.Http.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private static string BuildSupabaseUri(string? baseUrl, string resource, IEnumerable<KeyValuePair<string, string?>> queryParams)
        {
            var trimmedBase = (baseUrl ?? string.Empty).TrimEnd('/');
            if (string.IsNullOrEmpty(trimmedBase))
                throw new RpcException(RpcError.InternalServerError.WithData("supabase url not configured"));

            StringBuilder builder = new();
            builder.Append(trimmedBase);
            builder.Append("/rest/v1/");
            builder.Append(resource);

            bool first = true;
            foreach (var (key, value) in queryParams)
            {
                if (string.IsNullOrEmpty(value))
                    continue;
                builder.Append(first ? '?' : '&');
                first = false;
                builder.Append(Uri.EscapeDataString(key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(value));
            }

            return builder.ToString();
        }

        private static void ApplySupabaseHeaders(HttpRequestMessage request, string apiKey)
        {
            request.Headers.TryAddWithoutValidation("apikey", apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        }

        private static int? TryParseTotalCount(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Content-Range", out var values))
                return null;
            var raw = values.FirstOrDefault();
            if (string.IsNullOrEmpty(raw))
                return null;
            var slashIndex = raw.LastIndexOf('/');
            if (slashIndex < 0 || slashIndex == raw.Length - 1)
                return null;
            var totalPart = raw[(slashIndex + 1)..];
            if (totalPart == "*")
                return null;
            return int.TryParse(totalPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var total) ? total : null;
        }
    }
}

