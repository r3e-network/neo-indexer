// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceRecorder.Recording.Logs.cs file belongs to the neo project and is free
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
        /// Records a runtime log trace.
        /// </summary>
        public void RecordLog(LogTrace trace)
        {
            if (!IsEnabled) return;
            _logs.Enqueue(trace);
            Interlocked.Increment(ref _logCount);
        }

        /// <summary>
        /// Creates and records a runtime log trace with auto-incrementing order.
        /// </summary>
        public LogTrace RecordLog(UInt160 contractHash, string message)
        {
            var trace = new LogTrace
            {
                ContractHash = contractHash,
                Message = message,
                Order = Interlocked.Increment(ref _logOrder) - 1
            };

            if (IsEnabled)
            {
                _logs.Enqueue(trace);
                Interlocked.Increment(ref _logCount);
            }

            return trace;
        }
    }
}

