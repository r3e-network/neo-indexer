// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngine.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Collections.Generic;

namespace Neo.SmartContract
{
    /// <summary>
    /// ApplicationEngine implementation that emits extended traces for syscalls, storage mutations, and notifications.
    /// </summary>
    public class TracingApplicationEngine : ApplicationEngine
    {
        private readonly ExecutionTraceRecorder _traceRecorder;
        private readonly ExecutionTraceLevel _traceLevel;
        private readonly Dictionary<int, UInt160> _contractHashCache = new();

        /// <summary>
        /// Gets the recorder used to capture execution traces.
        /// </summary>
        public ExecutionTraceRecorder TraceRecorder => _traceRecorder;

        /// <summary>
        /// Gets the enabled trace level flags.
        /// </summary>
        public ExecutionTraceLevel TraceLevel => _traceLevel;

        /// <summary>
        /// Initializes a new instance of <see cref="TracingApplicationEngine"/>.
        /// </summary>
        public TracingApplicationEngine(
            TriggerType trigger,
            IVerifiable container,
            DataCache snapshotCache,
            Block? persistingBlock,
            ProtocolSettings settings,
            long gas,
            ExecutionTraceRecorder traceRecorder,
            ExecutionTraceLevel traceLevel,
            IDiagnostic? diagnostic = null,
            JumpTable? jumpTable = null)
            : base(trigger, container, snapshotCache, persistingBlock, settings, gas, diagnostic, jumpTable)
        {
            _traceRecorder = traceRecorder ?? throw new ArgumentNullException(nameof(traceRecorder));
            _traceLevel = traceLevel;
        }

        /// <summary>
        /// Handles syscall execution while emitting tracing information.
        /// </summary>
        protected override void OnSysCall(InteropDescriptor descriptor)
        {
            ValidateCallFlags(descriptor.RequiredCallFlags);
            long gasCost = descriptor.FixedPrice * ExecFeeFactor;
            AddFee(gasCost);

            object[] parameters = new object[descriptor.Parameters.Count];
            for (int i = 0; i < parameters.Length; i++)
                parameters[i] = Convert(Pop(), descriptor.Parameters[i]);

            StorageTraceScope? storageScope = PrepareStorageScope(descriptor, parameters);
            int notificationBaseline = ShouldTrace(ExecutionTraceLevel.Notifications) ? Notifications.Count : 0;

            if (ShouldTrace(ExecutionTraceLevel.Syscalls))
            {
                _traceRecorder.RecordSyscall(
                    CurrentScriptHash,
                    descriptor.Hash,
                    descriptor.Name,
                    gasCost);
            }

            object? returnValue = descriptor.Handler.Invoke(this, parameters);
            if (descriptor.Handler.ReturnType != typeof(void))
                Push(Convert(returnValue));

            if (storageScope is not null)
                RecordStorageScope(storageScope);

            if (ShouldTrace(ExecutionTraceLevel.Notifications))
                RecordNotifications(notificationBaseline);
        }

        private bool ShouldTrace(ExecutionTraceLevel level)
        {
            if (_traceRecorder is null || !_traceRecorder.IsEnabled)
                return false;
            return (_traceLevel & level) != 0;
        }

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
            if (SnapshotCache.TryGet(storageKey, out var existingItem))
                oldValue = Clone(existingItem.Value);

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
                StorageItem? updatedItem = SnapshotCache.TryGet(scope.StorageKey);
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

        private void RecordNotifications(int baseline)
        {
            var notificationSnapshot = Notifications;
            if (notificationSnapshot.Count <= baseline)
                return;

            for (int i = baseline; i < notificationSnapshot.Count; i++)
            {
                var notification = notificationSnapshot[i];
                string? stateJson = null;
                try
                {
                    stateJson = JsonSerializer.Serialize(notification.State)?.ToString();
                }
                catch
                {
                    stateJson = null;
                }

                _traceRecorder.RecordNotification(
                    notification.ScriptHash,
                    notification.EventName,
                    stateJson);
            }
        }

        private bool TryResolveContractHash(int contractId, out UInt160? hash)
        {
            if (_contractHashCache.TryGetValue(contractId, out var cached))
            {
                hash = cached;
                return true;
            }

            ContractState? contract = NativeContract.ContractManagement.GetContractById(SnapshotCache, contractId);
            if (contract is null)
            {
                hash = null;
                return false;
            }

            hash = contract.Hash;
            _contractHashCache[contractId] = hash;
            return true;
        }

        private static ReadOnlyMemory<byte> Clone(ReadOnlyMemory<byte> source)
        {
            return source.IsEmpty ? ReadOnlyMemory<byte>.Empty : source.ToArray();
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
