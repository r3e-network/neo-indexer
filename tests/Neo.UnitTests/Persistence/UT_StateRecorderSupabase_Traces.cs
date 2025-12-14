// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StateRecorderSupabase_Traces.cs file belongs to the neo project and is free
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
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Persistence
{
    [TestClass]
    public sealed partial class UT_StateRecorderSupabase_Traces
    {
        private const string EnvPrefix = "NEO_STATE_RECORDER__";
        private static readonly FieldInfo HttpClientField = typeof(StateRecorderSupabase).GetField("HttpClient", BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly UInt256 SampleTxHash = UInt256.Parse("0x11223344556677889900AABBCCDDEEFF00112233445566778899AABBCCDDEEFF");
        private static readonly UInt160 ExecutionContract = UInt160.Parse("0x0101010101010101010101010101010101010101");
        private static readonly UInt160 CallerContract = UInt160.Parse("0x0202020202020202020202020202020202020202");
        private static readonly UInt160 CalleeContract = UInt160.Parse("0x0303030303030303030303030303030303030303");
        private static readonly byte[] SampleOperand = new byte[] { 0x0A, 0x0B, 0x0C, 0x0D };
        private static readonly byte[] SampleStorageKey = Encoding.UTF8.GetBytes("trace-key");
        private static readonly byte[] SampleOldValue = new byte[] { 0x01, 0x02 };
        private static readonly byte[] SampleNewValue = new byte[] { 0x05, 0x06, 0x07 };

        private readonly Dictionary<string, string?> _envBackup = new(StringComparer.OrdinalIgnoreCase);
        private HttpClient? _originalHttpClient;
        private HttpClient? _testHttpClient;

        [TestInitialize]
        public void Initialize()
        {
            SetRecorderEnv("ENABLED", "true");
            SetRecorderEnv("SUPABASE_URL", "https://example.supabase.co/project/");
            SetRecorderEnv("SUPABASE_KEY", "test-api-key");
            SetRecorderEnv("UPLOAD_MODE", "RestApi");
            SetRecorderEnv("TRACE_BATCH_SIZE", null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            RestoreHttpClient();
            foreach (var kv in _envBackup)
            {
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
            _envBackup.Clear();
        }

        private static ExecutionTraceRecorder CreateRecorder()
        {
            return new ExecutionTraceRecorder
            {
                TxHash = SampleTxHash
            };
        }

        private static ExecutionTraceRecorder CreateRecorderWithFullTraces()
        {
            var recorder = CreateRecorder();
            recorder.RecordOpCode(ExecutionContract, 42, OpCode.PUSH1, SampleOperand, 1500, 3);
            recorder.RecordSyscall(ExecutionContract, 0x01020304, "System.Storage.Get", 64);

            var contractCall = recorder.RecordContractCall(CallerContract, CalleeContract, "transfer", 2);
            contractCall.Success = false;
            contractCall.GasConsumed = 3210;

            var storageTrace = new StorageWriteTrace
            {
                ContractId = 77,
                ContractHash = ExecutionContract,
                Key = SampleStorageKey,
                OldValue = new ReadOnlyMemory<byte>(SampleOldValue),
                NewValue = new ReadOnlyMemory<byte>(SampleNewValue),
                Order = 0
            };
            recorder.RecordStorageWrite(storageTrace);

            recorder.RecordNotification(ExecutionContract, "Transfer", "{\"type\":\"Integer\",\"value\":\"1\"}");
            return recorder;
        }

        private static ExecutionTraceRecorder CreateRecorderWithOpCodes(int count)
        {
            var recorder = CreateRecorder();
            for (var i = 0; i < count; i++)
            {
                recorder.RecordOpCode(ExecutionContract, i, OpCode.PUSH1, ReadOnlyMemory<byte>.Empty, i, i % 4);
            }
            return recorder;
        }

        private void SetRecorderEnv(string suffix, string? value)
        {
            var name = EnvPrefix + suffix;
            if (!_envBackup.ContainsKey(name))
            {
                _envBackup[name] = Environment.GetEnvironmentVariable(name);
            }
            Environment.SetEnvironmentVariable(name, value);
        }

        private void OverrideHttpClient(HttpClient client)
        {
            _originalHttpClient ??= (HttpClient)HttpClientField.GetValue(null)!;
            HttpClientField.SetValue(null, client);
            _testHttpClient = client;
        }

        private void RestoreHttpClient()
        {
            if (_testHttpClient != null && _originalHttpClient != null)
            {
                HttpClientField.SetValue(null, _originalHttpClient);
                _testHttpClient.Dispose();
                _testHttpClient = null;
            }
        }

        private Mock<HttpMessageHandler> ConfigureHandler(Func<HttpResponseMessage> responseFactory, Action<HttpRequestMessage>? onRequest = null)
        {
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage _, CancellationToken _) => responseFactory())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => onRequest?.Invoke(request));

            OverrideHttpClient(new HttpClient(handler.Object));
            return handler;
        }

        private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string body = "{}")
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        private sealed record CapturedRequest(string Table, string Path, string Payload, HttpMethod Method, string ContentType, IReadOnlyDictionary<string, string> Headers)
        {
            public static CapturedRequest Create(HttpRequestMessage request)
            {
                var payload = request.Content is null
                    ? string.Empty
                    : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in request.Headers)
                {
                    headers[header.Key] = string.Join(",", header.Value);
                }

                if (request.Content is not null)
                {
                    foreach (var header in request.Content.Headers)
                    {
                        headers[header.Key] = string.Join(",", header.Value);
                    }
                }

                var path = request.RequestUri!.AbsolutePath;
                var table = path[(path.LastIndexOf('/') + 1)..];
                var mediaType = request.Content?.Headers.ContentType?.MediaType ?? string.Empty;
                return new CapturedRequest(table, path, payload, request.Method, mediaType, headers);
            }

            public bool TryGetHeader(string name, out string? value) => Headers.TryGetValue(name, out value);
        }
    }
}

