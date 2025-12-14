// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceRecorder.Recording.Notifications.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Threading;

namespace Neo.Persistence
{
    public sealed partial class ExecutionTraceRecorder
    {
        /// <summary>
        /// Records a notification trace.
        /// </summary>
        public void RecordNotification(NotificationTrace trace)
        {
            if (!IsEnabled) return;
            _notifications.Enqueue(trace);
            Interlocked.Increment(ref _notificationCount);
        }

        /// <summary>
        /// Creates and records a notification trace with auto-incrementing order.
        /// </summary>
        public NotificationTrace RecordNotification(
            UInt160 contractHash,
            string eventName,
            string? stateJson)
        {
            var trace = new NotificationTrace
            {
                ContractHash = contractHash,
                EventName = eventName,
                StateJson = stateJson,
                Order = Interlocked.Increment(ref _notificationOrder) - 1
            };

            if (IsEnabled)
            {
                _notifications.Enqueue(trace);
                Interlocked.Increment(ref _notificationCount);
            }

            return trace;
        }
    }
}

