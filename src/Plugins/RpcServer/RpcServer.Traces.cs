// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private const int DefaultTraceLimit = 1000;
        private const int MaxTraceLimit = 5000;
        private const string RpcTracesSupabaseKeyEnvVar = "NEO_RPC_TRACES__SUPABASE_KEY";
        private const string RpcTracesConcurrencyEnvVar = "NEO_RPC_TRACES__MAX_CONCURRENCY";
        private const int DefaultRpcTracesConcurrency = 16;

        private static readonly int TraceQueryConcurrency = GetTraceQueryConcurrency();
        private static readonly SemaphoreSlim TraceQuerySemaphore = new(TraceQueryConcurrency, TraceQueryConcurrency);
        private static readonly HttpClient TraceHttpClient = CreateTraceHttpClient();
        private static readonly JsonSerializerOptions TraceSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static int GetTraceQueryConcurrency()
        {
            var raw = Environment.GetEnvironmentVariable(RpcTracesConcurrencyEnvVar);
            if (string.IsNullOrWhiteSpace(raw))
                return DefaultRpcTracesConcurrency;

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return DefaultRpcTracesConcurrency;

            return parsed > 0 ? parsed : DefaultRpcTracesConcurrency;
        }

        private static HttpClient CreateTraceHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            return new HttpClient(handler);
        }
    }
}

