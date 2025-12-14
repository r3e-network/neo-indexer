// Copyright (C) 2015-2025 The Neo Project.
//
// UT_TracingApplicationEngine.StateReadRecorder.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.UnitTests.Extensions;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.UnitTests.SmartContract
{
    public partial class UT_TracingApplicationEngine
    {
        [TestMethod]
        public void TracingApplicationEngine_StorageTracing_DoesNotPolluteStateReadRecorder()
        {
            // Arrange: A contract that writes a key that already exists in storage.
            var keyBytes = new byte[] { 0x01 };
            var oldValue = new byte[] { 0xAA };
            var newValue = new byte[] { 0xBB };

            var script = CreatePutScript(keyBytes, newValue);
            var contract = TestUtils.GetContract(script);

            using var store = new MemoryStore();
            SeedContractAndStorage(store, contract, keyBytes, oldValue);

            var expectedStorageKey = new StorageKey { Id = contract.Id, Key = keyBytes };
            var expectedStorageKeyBase64 = Convert.ToBase64String(expectedStorageKey.ToArray());

            // Act: Execute the same script with and without Storage trace level.
            var baselineKeys = ExecuteAndCaptureReadKeys(store, script, ExecutionTraceLevel.Syscalls);
            var storageTraceKeys = ExecuteAndCaptureReadKeys(store, script, ExecutionTraceLevel.Syscalls | ExecutionTraceLevel.Storage);

            // Assert: Storage tracing does not add extra recorded reads (e.g., contract metadata lookups),
            // and does not suppress legitimate reads (e.g., existing key old-value reads).
            Assert.IsTrue(baselineKeys.Count > 0, "Expected at least one recorded read key in the baseline run.");
            Assert.IsTrue(baselineKeys.Contains(expectedStorageKeyBase64), "Expected baseline to record the existing storage key read.");
            Assert.IsTrue(storageTraceKeys.Contains(expectedStorageKeyBase64), "Expected storage tracing to still record the existing storage key read.");
            Assert.IsTrue(
                baselineKeys.SetEquals(storageTraceKeys),
                $"Expected identical recorded read keys, but baseline={baselineKeys.Count} storageTracing={storageTraceKeys.Count}.");
        }

        private static byte[] CreatePutScript(byte[] key, byte[] value)
        {
            using var scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitPush(value);
            scriptBuilder.EmitPush(key);
            scriptBuilder.EmitSysCall(ApplicationEngine.System_Storage_GetContext);
            scriptBuilder.EmitSysCall(ApplicationEngine.System_Storage_Put);
            return scriptBuilder.ToArray();
        }

        private static void SeedContractAndStorage(MemoryStore store, ContractState contract, byte[] keyBytes, byte[] valueBytes)
        {
            using var snapshot = store.GetSnapshot();
            using var cache = new StoreCache(snapshot);

            cache.AddContract(contract.Hash, contract);
            cache.Add(new StorageKey { Id = contract.Id, Key = keyBytes }, new StorageItem(valueBytes));
            cache.Commit();
        }

        private HashSet<string> ExecuteAndCaptureReadKeys(MemoryStore store, byte[] script, ExecutionTraceLevel traceLevel)
        {
            using var snapshot = new RecordingStoreSnapshot(store.GetSnapshot());
            using var snapshotCache = new StoreCache(snapshot);

            var block = _persistingBlock;
            var readRecorder = new BlockReadRecorder(block, maxEntries: 0);
            using var scope = new BlockReadRecorderScope(readRecorder, previous: null);

            var traceRecorder = new ExecutionTraceRecorder();
            using var engine = new TracingApplicationEngine(
                TriggerType.Application,
                null!,
                snapshotCache.CloneCache(),
                block,
                TestProtocolSettings.Default,
                ApplicationEngine.TestModeGas,
                traceRecorder,
                traceLevel);

            engine.LoadScript(script);
            var state = engine.Execute();
            Assert.AreEqual(VMState.HALT, state);

            return readRecorder.Entries
                .Select(e => Convert.ToBase64String(e.Key.ToArray()))
                .ToHashSet(StringComparer.Ordinal);
        }
    }
}
