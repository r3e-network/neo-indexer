// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Types.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Generic;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private sealed class TraceRequestOptions
        {
            public int Limit { get; set; } = DefaultTraceLimit;
            public int Offset { get; set; }
            public string? TransactionHash { get; set; }
        }

        private enum ContractCallRole
        {
            Any,
            Caller,
            Callee
        }

        private sealed class ContractCallQueryOptions
        {
            public int Limit { get; set; } = DefaultTraceLimit;
            public int Offset { get; set; }
            public uint? StartBlock { get; set; }
            public uint? EndBlock { get; set; }
            public string? TransactionHash { get; set; }
            public ContractCallRole Role { get; set; } = ContractCallRole.Any;
        }

        private sealed class SyscallStatsQueryOptions
        {
            public int Limit { get; set; } = DefaultTraceLimit;
            public int Offset { get; set; }
            public uint? StartBlock { get; set; }
            public uint? EndBlock { get; set; }
            public string? ContractHash { get; set; }
            public string? TransactionHash { get; set; }
            public string? SyscallName { get; set; }
        }

        private sealed class OpCodeStatsQueryOptions
        {
            public int Limit { get; set; } = DefaultTraceLimit;
            public int Offset { get; set; }
            public uint? StartBlock { get; set; }
            public uint? EndBlock { get; set; }
            public string? ContractHash { get; set; }
            public string? TransactionHash { get; set; }
            public int? OpCode { get; set; }
            public string? OpCodeName { get; set; }
        }

        private sealed class ContractCallStatsQueryOptions
        {
            public int Limit { get; set; } = DefaultTraceLimit;
            public int Offset { get; set; }
            public uint? StartBlock { get; set; }
            public uint? EndBlock { get; set; }
            public string? CalleeHash { get; set; }
            public string? CallerHash { get; set; }
            public string? MethodName { get; set; }
        }

        private sealed class SupabaseResponse<T>
        {
            public SupabaseResponse(IReadOnlyList<T> items, int totalCount)
            {
                Items = items;
                TotalCount = totalCount;
            }

            public IReadOnlyList<T> Items { get; }
            public int? TotalCount { get; }
        }
    }
}

