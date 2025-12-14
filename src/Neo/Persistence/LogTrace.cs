// Copyright (C) 2015-2025 The Neo Project.
//
// LogTrace.cs file belongs to the neo project and is free
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
    /// Represents a runtime log trace entry (System.Runtime.Log).
    /// </summary>
    public readonly record struct LogTrace
    {
        /// <summary>
        /// The contract hash emitting the log.
        /// </summary>
        public UInt160 ContractHash { get; init; }

        /// <summary>
        /// The message payload.
        /// </summary>
        public string Message { get; init; }

        /// <summary>
        /// Log order within the transaction.
        /// </summary>
        public int Order { get; init; }
    }
}

