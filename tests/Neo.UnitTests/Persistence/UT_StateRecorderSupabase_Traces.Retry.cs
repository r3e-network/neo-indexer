// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StateRecorderSupabase_Traces.Retry.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Persistence
{
    public sealed partial class UT_StateRecorderSupabase_Traces
    {
        [TestMethod]
        [Ignore(".NET 9 disallows setting initonly static fields via reflection")]
        public async Task UploadBlockTraceAsync_RetriesWhenRateLimited()
        {
            var recorder = CreateRecorderWithOpCodes(1);
            var responses = new Queue<HttpResponseMessage>(new[]
            {
                CreateResponse(HttpStatusCode.TooManyRequests, "\"retry\""),
                CreateResponse(HttpStatusCode.OK)
            });

            var handler = ConfigureHandler(() => responses.Dequeue());
            await StateRecorderSupabase.UploadBlockTraceAsync(1, recorder);

            handler.Protected().Verify(
                "SendAsync",
                Times.Exactly(2),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
            Assert.AreEqual(0, responses.Count, "All queued responses should be consumed.");
        }

        [TestMethod]
        [Ignore(".NET 9 disallows setting initonly static fields via reflection")]
        public async Task UploadBlockTraceAsync_RetriesWhenServiceUnavailable()
        {
            var recorder = CreateRecorderWithOpCodes(1);
            var responses = new Queue<HttpResponseMessage>(new[]
            {
                CreateResponse(HttpStatusCode.ServiceUnavailable, "\"retry\""),
                CreateResponse(HttpStatusCode.OK)
            });

            var handler = ConfigureHandler(() => responses.Dequeue());
            await StateRecorderSupabase.UploadBlockTraceAsync(2, recorder);

            handler.Protected().Verify(
                "SendAsync",
                Times.Exactly(2),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
            Assert.AreEqual(0, responses.Count);
        }

        [TestMethod]
        [Ignore(".NET 9 disallows setting initonly static fields via reflection")]
        public async Task UploadBlockTraceAsync_UsesExponentialBackoffDelays()
        {
            var recorder = CreateRecorderWithOpCodes(1);
            var responses = new Queue<HttpResponseMessage>(new[]
            {
                CreateResponse(HttpStatusCode.TooManyRequests, "\"delay\""),
                CreateResponse(HttpStatusCode.TooManyRequests, "\"delay\""),
                CreateResponse(HttpStatusCode.OK)
            });

            var handler = ConfigureHandler(() => responses.Dequeue());
            var stopwatch = Stopwatch.StartNew();
            await StateRecorderSupabase.UploadBlockTraceAsync(3, recorder);
            stopwatch.Stop();

            handler.Protected().Verify(
                "SendAsync",
                Times.Exactly(3),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
            Assert.IsTrue(stopwatch.Elapsed >= TimeSpan.FromSeconds(3), $"Backoff should wait at least 3 seconds, elapsed {stopwatch.Elapsed}.");
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(6), "Backoff should not exceed 6 seconds for two retries.");
        }
    }
}

