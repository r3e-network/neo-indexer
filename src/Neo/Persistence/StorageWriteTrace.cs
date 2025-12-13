// Copyright (C) 2015-2025 The Neo Project.
//
// StorageWriteTrace.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;

namespace Neo.Persistence
{
    /// <summary>
    /// Represents a storage write operation trace entry.
    /// </summary>
    public readonly record struct StorageWriteTrace
    {
        /// <summary>
        /// The contract ID performing the write.
        /// </summary>
        public int ContractId { get; init; }

        /// <summary>
        /// The contract hash performing the write.
        /// </summary>
        public UInt160 ContractHash { get; init; }

        /// <summary>
        /// The storage key being written.
        /// </summary>
        public ReadOnlyMemory<byte> Key { get; init; }

        /// <summary>
        /// The old value (null if new key).
        /// </summary>
        public ReadOnlyMemory<byte>? OldValue { get; init; }

        /// <summary>
        /// The new value being written.
        /// </summary>
        public ReadOnlyMemory<byte> NewValue { get; init; }

        /// <summary>
        /// Write order within the transaction.
        /// </summary>
        public int Order { get; init; }
    }
}

