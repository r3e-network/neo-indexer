// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceRecorder.Recording.ContractCalls.cs file belongs to the neo project and is free
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
        /// Records a contract call trace.
        /// </summary>
        public void RecordContractCall(ContractCallTrace trace)
        {
            if (!IsEnabled) return;
            _contractCalls.Enqueue(trace);
            Interlocked.Increment(ref _contractCallCount);
        }

        /// <summary>
        /// Creates and records a contract call trace with auto-incrementing order.
        /// </summary>
        public ContractCallTrace RecordContractCall(
            UInt160? callerHash,
            UInt160 calleeHash,
            string? methodName,
            int callDepth)
        {
            var trace = new ContractCallTrace
            {
                CallerHash = callerHash,
                CalleeHash = calleeHash,
                MethodName = methodName,
                CallDepth = callDepth,
                Order = Interlocked.Increment(ref _contractCallOrder) - 1
            };

            if (IsEnabled)
            {
                _contractCalls.Enqueue(trace);
                Interlocked.Increment(ref _contractCallCount);
            }

            return trace;
        }
    }
}

