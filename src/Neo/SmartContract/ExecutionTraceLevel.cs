// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceLevel.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;

namespace Neo.SmartContract
{
    /// <summary>
    /// Flags representing which parts of execution should be captured by tracing components.
    /// </summary>
    [Flags]
    public enum ExecutionTraceLevel
    {
        None = 0,
        Syscalls = 1 << 0,
        Storage = 1 << 1,
        Notifications = 1 << 2,
        ContractCalls = 1 << 3,
        OpCodes = 1 << 4,
        Logs = 1 << 5,
        All = Syscalls | Storage | Notifications | ContractCalls | OpCodes | Logs
    }
}
