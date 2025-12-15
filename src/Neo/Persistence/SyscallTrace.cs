// Copyright (C) 2015-2025 The Neo Project.
//
// SyscallTrace.cs file belongs to the neo project and is free
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
    /// Represents a syscall invocation trace entry.
    /// Captured via ApplicationEngine.OnSysCall override.
    /// </summary>
    public readonly record struct SyscallTrace
    {
        /// <summary>
        /// The contract hash invoking this syscall.
        /// </summary>
        public UInt160 ContractHash { get; init; }

        /// <summary>
        /// The syscall hash (uint32 as hex string).
        /// </summary>
        public string SyscallHash { get; init; }

        /// <summary>
        /// Human-readable syscall name (e.g., "System.Storage.Get").
        /// </summary>
        public string SyscallName { get; init; }

        /// <summary>
        /// GAS cost of this syscall (in datoshi).
        /// </summary>
        public long GasCost { get; init; }

        /// <summary>
        /// True when the syscall handler returned without throwing.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Execution order within the transaction.
        /// </summary>
        public int Order { get; init; }
    }
}
