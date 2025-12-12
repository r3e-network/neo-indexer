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
using Neo;
using Neo.Persistence;
using Neo.VM;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Persistence
{
    [TestClass]
    public sealed class UT_StateRecorderSupabase_Traces
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

        [TestMethod]
        [Ignore(".NET 9 disallows setting initonly static fields via reflection")]
        public async Task UploadBlockTraceAsync_SendsExpectedRestPayloads()
        {
            var recorder = CreateRecorderWithFullTraces();
            var captured = new ConcurrentBag<CapturedRequest>();
            _ = ConfigureHandler(() => CreateResponse(HttpStatusCode.OK), request => captured.Add(CapturedRequest.Create(request)));

            await StateRecorderSupabase.UploadBlockTraceAsync(123u, recorder);

            var requests = captured.ToList();
            Assert.AreEqual(5, requests.Count, "Expected one REST request per trace table.");
            foreach (var request in requests)
            {
                StringAssert.Contains(request.Path, "/rest/v1/");
                Assert.AreEqual(HttpMethod.Post, request.Method);
                Assert.IsTrue(string.Equals("application/json", request.ContentType, StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(request.TryGetHeader("Prefer", out var prefer) && prefer == "resolution=merge-duplicates");
                Assert.IsTrue(request.TryGetHeader("apikey", out var apiKey) && apiKey == "test-api-key");
                Assert.IsTrue(request.TryGetHeader("Authorization", out var auth) && auth == "Bearer test-api-key");
            }

            var tableMap = requests.ToDictionary(r => r.Table);
            Assert.IsTrue(tableMap.ContainsKey("opcode_traces"));
            Assert.IsTrue(tableMap.ContainsKey("syscall_traces"));
            Assert.IsTrue(tableMap.ContainsKey("contract_calls"));
            Assert.IsTrue(tableMap.ContainsKey("storage_writes"));
            Assert.IsTrue(tableMap.ContainsKey("notifications"));

            var txHash = SampleTxHash.ToString();
            using (var opcodeDoc = JsonDocument.Parse(tableMap["opcode_traces"].Payload))
            {
                var opcode = opcodeDoc.RootElement.EnumerateArray().Single();
                Assert.AreEqual(123, opcode.GetProperty("block_index").GetInt32());
                Assert.AreEqual(txHash, opcode.GetProperty("tx_hash").GetString());
                Assert.AreEqual(0, opcode.GetProperty("trace_order").GetInt32());
                Assert.AreEqual(ExecutionContract.ToString(), opcode.GetProperty("contract_hash").GetString());
                Assert.AreEqual(Convert.ToBase64String(SampleOperand), opcode.GetProperty("operand_base64").GetString());
                Assert.AreEqual(3, opcode.GetProperty("stack_depth").GetInt32());
            }

            using (var syscallDoc = JsonDocument.Parse(tableMap["syscall_traces"].Payload))
            {
                var syscall = syscallDoc.RootElement.EnumerateArray().Single();
                Assert.AreEqual("System.Storage.Get", syscall.GetProperty("syscall_name").GetString());
                Assert.AreEqual("01020304", syscall.GetProperty("syscall_hash").GetString());
                Assert.AreEqual(ExecutionContract.ToString(), syscall.GetProperty("contract_hash").GetString());
            }

            using (var callDoc = JsonDocument.Parse(tableMap["contract_calls"].Payload))
            {
                var call = callDoc.RootElement.EnumerateArray().Single();
                Assert.AreEqual(CallerContract.ToString(), call.GetProperty("caller_hash").GetString());
                Assert.AreEqual(CalleeContract.ToString(), call.GetProperty("callee_hash").GetString());
                Assert.AreEqual("transfer", call.GetProperty("method_name").GetString());
                Assert.AreEqual(2, call.GetProperty("call_depth").GetInt32());
                Assert.IsFalse(call.GetProperty("success").GetBoolean());
                Assert.AreEqual(3210, call.GetProperty("gas_consumed").GetInt64());
            }

            using (var storageDoc = JsonDocument.Parse(tableMap["storage_writes"].Payload))
            {
                var write = storageDoc.RootElement.EnumerateArray().Single();
                Assert.AreEqual(0, write.GetProperty("write_order").GetInt32());
                Assert.AreEqual(77, write.GetProperty("contract_id").GetInt32());
                Assert.AreEqual(Convert.ToBase64String(SampleStorageKey), write.GetProperty("key_base64").GetString());
                Assert.AreEqual(Convert.ToBase64String(SampleOldValue), write.GetProperty("old_value_base64").GetString());
                Assert.AreEqual(Convert.ToBase64String(SampleNewValue), write.GetProperty("new_value_base64").GetString());
            }

            using (var notificationDoc = JsonDocument.Parse(tableMap["notifications"].Payload))
            {
                var notification = notificationDoc.RootElement.EnumerateArray().Single();
                var state = notification.GetProperty("state_json").GetProperty("type").GetString();
                Assert.AreEqual("Integer", state);
            }
        }

        [TestMethod]
        [Ignore(".NET 9 disallows setting initonly static fields via reflection")]
        public async Task UploadBlockTraceAsync_SplitsLargeOpcodeBatches()
        {
            SetRecorderEnv("TRACE_BATCH_SIZE", "6000");
            const int totalRows = 10_123;
            var recorder = CreateRecorderWithOpCodes(totalRows);
            var batchSizes = new List<int>();

            var handler = ConfigureHandler(() => CreateResponse(HttpStatusCode.OK), request =>
            {
                StringAssert.EndsWith(request.RequestUri!.AbsolutePath, "/rest/v1/opcode_traces");
                var payload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using var payloadDoc = JsonDocument.Parse(payload);
                batchSizes.Add(payloadDoc.RootElement.GetArrayLength());
            });

            await StateRecorderSupabase.UploadBlockTraceAsync(999u, recorder);

            Assert.AreEqual(3, batchSizes.Count, "Expected 3 batches (5000 + 5000 + remainder).");
            var expected = new[] { 5000, 5000, totalRows - 10_000 };
            CollectionAssert.AreEquivalent(expected, batchSizes);
            handler.Protected().Verify(
                "SendAsync",
                Times.Exactly(3),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

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

        [TestMethod]
        public void TraceRowModels_SerializeWithExpectedPropertyNames()
        {
            var txHash = SampleTxHash.ToString();
            var opcodeRow = new OpCodeTraceRow(100, txHash, 5, ExecutionContract.ToString(), 42, (int)OpCode.PUSH1, "PUSH1", "AQ==", 250, 2);
            var syscallRow = new SyscallTraceRow(100, txHash, 6, ExecutionContract.ToString(), "ABCD1234", "System.Runtime.Log", 77);
            var contractRow = new ContractCallTraceRow(100, txHash, 7, CallerContract.ToString(), CalleeContract.ToString(), "transfer", 2, false, 1500);

            using (var opcodeJson = JsonDocument.Parse(JsonSerializer.Serialize(opcodeRow)))
            {
                var element = opcodeJson.RootElement;
                Assert.AreEqual(100, element.GetProperty("block_index").GetInt32());
                Assert.AreEqual("PUSH1", element.GetProperty("opcode_name").GetString());
                Assert.AreEqual("AQ==", element.GetProperty("operand_base64").GetString());
                Assert.AreEqual(2, element.GetProperty("stack_depth").GetInt32());
            }

            using (var syscallJson = JsonDocument.Parse(JsonSerializer.Serialize(syscallRow)))
            {
                var element = syscallJson.RootElement;
                Assert.AreEqual("ABCD1234", element.GetProperty("syscall_hash").GetString());
                Assert.AreEqual("System.Runtime.Log", element.GetProperty("syscall_name").GetString());
                Assert.AreEqual(77, element.GetProperty("gas_cost").GetInt64());
            }

            using (var contractJson = JsonDocument.Parse(JsonSerializer.Serialize(contractRow)))
            {
                var element = contractJson.RootElement;
                Assert.AreEqual("transfer", element.GetProperty("method_name").GetString());
                Assert.IsFalse(element.GetProperty("success").GetBoolean());
                Assert.AreEqual(1500, element.GetProperty("gas_consumed").GetInt64());
                Assert.AreEqual(CallerContract.ToString(), element.GetProperty("caller_hash").GetString());
                Assert.AreEqual(CalleeContract.ToString(), element.GetProperty("callee_hash").GetString());
            }
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
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => responseFactory())
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
