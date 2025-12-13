// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceRecorder.Accessors.cs file belongs to the neo project and is free
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

namespace Neo.Persistence
{
    public sealed partial class ExecutionTraceRecorder
    {
        /// <summary>
        /// Gets all recorded OpCode traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<OpCodeTrace> GetOpCodeTraces()
        {
            var snapshot = _opCodeTraces.ToArray();
            for (var i = 1; i < snapshot.Length; i++)
            {
                if (snapshot[i].Order < snapshot[i - 1].Order)
                {
                    Array.Sort(snapshot, static (a, b) => a.Order.CompareTo(b.Order));
                    break;
                }
            }
            return snapshot;
        }

        /// <summary>
        /// Gets all recorded syscall traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<SyscallTrace> GetSyscallTraces()
        {
            var snapshot = _syscallTraces.ToArray();
            for (var i = 1; i < snapshot.Length; i++)
            {
                if (snapshot[i].Order < snapshot[i - 1].Order)
                {
                    Array.Sort(snapshot, static (a, b) => a.Order.CompareTo(b.Order));
                    break;
                }
            }
            return snapshot;
        }

        /// <summary>
        /// Gets all recorded contract call traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<ContractCallTrace> GetContractCallTraces()
        {
            var snapshot = _contractCalls.ToArray();
            for (var i = 1; i < snapshot.Length; i++)
            {
                if (snapshot[i].Order < snapshot[i - 1].Order)
                {
                    Array.Sort(snapshot, static (a, b) => a.Order.CompareTo(b.Order));
                    break;
                }
            }
            return snapshot;
        }

        /// <summary>
        /// Gets all recorded storage write traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<StorageWriteTrace> GetStorageWriteTraces()
        {
            var snapshot = _storageWrites.ToArray();
            for (var i = 1; i < snapshot.Length; i++)
            {
                if (snapshot[i].Order < snapshot[i - 1].Order)
                {
                    Array.Sort(snapshot, static (a, b) => a.Order.CompareTo(b.Order));
                    break;
                }
            }
            return snapshot;
        }

        /// <summary>
        /// Gets all recorded notification traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<NotificationTrace> GetNotificationTraces()
        {
            var snapshot = _notifications.ToArray();
            for (var i = 1; i < snapshot.Length; i++)
            {
                if (snapshot[i].Order < snapshot[i - 1].Order)
                {
                    Array.Sort(snapshot, static (a, b) => a.Order.CompareTo(b.Order));
                    break;
                }
            }
            return snapshot;
        }

        /// <summary>
        /// Returns true if any traces have been recorded.
        /// </summary>
        public bool HasTraces =>
            !_opCodeTraces.IsEmpty ||
            !_syscallTraces.IsEmpty ||
            !_contractCalls.IsEmpty ||
            !_storageWrites.IsEmpty ||
            !_notifications.IsEmpty;
    }
}

