// Copyright (C) 2015-2025 The Neo Project.
//
// ContractCallTrace.cs file belongs to the neo project and is free
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
    /// Represents a contract-to-contract call trace entry.
    /// Captured via IDiagnostic.ContextLoaded/ContextUnloaded.
    /// </summary>
    public sealed class ContractCallTrace
    {
        /// <summary>
        /// The calling contract hash (null for entry point).
        /// </summary>
        public UInt160? CallerHash { get; init; }

        /// <summary>
        /// The called contract hash.
        /// </summary>
        public UInt160 CalleeHash { get; init; } = UInt160.Zero;

        /// <summary>
        /// The method name being called (if available).
        /// </summary>
        public string? MethodName { get; init; }

        /// <summary>
        /// Call stack depth (1 = entry point).
        /// </summary>
        public int CallDepth { get; init; }

        /// <summary>
        /// Execution order within the transaction.
        /// </summary>
        public int Order { get; init; }

        /// <summary>
        /// Whether the call completed successfully.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// GAS consumed by this call (set on completion).
        /// </summary>
        public long GasConsumed { get; set; }
    }
}

