// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceRecorder.Recording.StorageWrites.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Threading;

namespace Neo.Persistence
{
    public sealed partial class ExecutionTraceRecorder
    {
        /// <summary>
        /// Records a storage write trace.
        /// </summary>
        public void RecordStorageWrite(StorageWriteTrace trace)
        {
            if (!IsEnabled) return;
            _storageWrites.Enqueue(trace);
            Interlocked.Increment(ref _storageWriteCount);
        }

        /// <summary>
        /// Creates and records a storage write trace with auto-incrementing order.
        /// </summary>
        public StorageWriteTrace RecordStorageWrite(
            int contractId,
            UInt160 contractHash,
            ReadOnlyMemory<byte> key,
            ReadOnlyMemory<byte>? oldValue,
            ReadOnlyMemory<byte> newValue,
            bool isDelete = false)
        {
            var trace = new StorageWriteTrace
            {
                ContractId = contractId,
                ContractHash = contractHash,
                IsDelete = isDelete,
                Key = key,
                OldValue = oldValue,
                NewValue = newValue,
                Order = Interlocked.Increment(ref _storageWriteOrder) - 1
            };

            if (IsEnabled)
            {
                _storageWrites.Enqueue(trace);
                Interlocked.Increment(ref _storageWriteCount);
            }

            return trace;
        }
    }
}
