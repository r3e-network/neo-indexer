// Copyright (C) 2015-2025 The Neo Project.
//
// NotificationTrace.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

namespace Neo.Persistence
{
    /// <summary>
    /// Represents a notification event trace entry.
    /// </summary>
    public readonly record struct NotificationTrace
    {
        /// <summary>
        /// The contract hash emitting the notification.
        /// </summary>
        public UInt160 ContractHash { get; init; }

        /// <summary>
        /// The event name.
        /// </summary>
        public string EventName { get; init; }

        /// <summary>
        /// The notification state as JSON.
        /// </summary>
        public string? StateJson { get; init; }

        /// <summary>
        /// Notification order within the transaction.
        /// </summary>
        public int Order { get; init; }
    }
}

