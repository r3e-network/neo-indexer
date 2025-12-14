// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngine.Storage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;
using Neo.SmartContract.Native;
using System;

namespace Neo.SmartContract
{
    public partial class TracingApplicationEngine
    {
        private StorageTraceScope? PrepareStorageScope(InteropDescriptor descriptor, object[] parameters)
        {
            if (!ShouldTrace(ExecutionTraceLevel.Storage))
                return null;

            bool isPut = descriptor == System_Storage_Put;
            bool isDelete = descriptor == System_Storage_Delete;
            if (!isPut && !isDelete)
                return null;

            var context = (StorageContext)parameters[0];
            var keyBytes = (byte[])parameters[1];
            StorageKey storageKey = new()
            {
                Id = context.Id,
                Key = keyBytes
            };

            ReadOnlyMemory<byte>? oldValue = null;
            using (StateReadRecorder.SuppressRecordingScope())
            {
                if (SnapshotCache.TryGet(storageKey, out var existingItem))
                    oldValue = Clone(existingItem.Value);
            }

            UInt160 contractHash = TryResolveContractHash(context.Id, out var hash) && hash is not null
                ? hash
                : UInt160.Zero;

            return new StorageTraceScope(
                context.Id,
                contractHash,
                storageKey,
                storageKey.Key,
                oldValue,
                isDelete);
        }

        private void RecordStorageScope(StorageTraceScope scope)
        {
            if (scope.IsDelete && scope.OldValue is null)
                return;

            ReadOnlyMemory<byte> newValue;
            if (scope.IsDelete)
            {
                newValue = ReadOnlyMemory<byte>.Empty;
            }
            else
            {
                StorageItem? updatedItem;
                using (StateReadRecorder.SuppressRecordingScope())
                {
                    updatedItem = SnapshotCache.TryGet(scope.StorageKey);
                }
                newValue = updatedItem is null
                    ? ReadOnlyMemory<byte>.Empty
                    : Clone(updatedItem.Value);
            }

            _traceRecorder.RecordStorageWrite(
                scope.ContractId,
                scope.ContractHash,
                scope.Key,
                scope.OldValue,
                newValue);
        }

        private bool TryResolveContractHash(int contractId, out UInt160? hash)
        {
            if (_contractHashCache.TryGetValue(contractId, out var cached))
            {
                hash = cached;
                return true;
            }

            // Contract metadata lookups are tracer-internal and should not be treated as user storage reads.
            ContractState? contract;
            using (StateReadRecorder.SuppressRecordingScope())
            {
                contract = NativeContract.ContractManagement.GetContractById(SnapshotCache, contractId);
            }
            if (contract is null)
            {
                hash = null;
                return false;
            }

            hash = contract.Hash;
            _contractHashCache[contractId] = hash;
            return true;
        }

        private sealed record StorageTraceScope(
            int ContractId,
            UInt160 ContractHash,
            StorageKey StorageKey,
            ReadOnlyMemory<byte> Key,
            ReadOnlyMemory<byte>? OldValue,
            bool IsDelete);
    }
}
