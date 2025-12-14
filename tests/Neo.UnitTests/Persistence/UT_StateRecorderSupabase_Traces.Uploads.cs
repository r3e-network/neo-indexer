// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StateRecorderSupabase_Traces.Uploads.cs file belongs to the neo project and is free
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.UnitTests.Persistence
{
    public sealed partial class UT_StateRecorderSupabase_Traces
    {
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
    }
}
