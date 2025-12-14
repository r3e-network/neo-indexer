// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceRecorder.Accessors.Logs.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Collections.Generic;

namespace Neo.Persistence
{
    public sealed partial class ExecutionTraceRecorder
    {
        /// <summary>
        /// Gets all recorded runtime log traces ordered by execution order.
        /// </summary>
        public IReadOnlyList<LogTrace> GetLogTraces()
        {
            var snapshot = _logs.ToArray();
            for (var i = 1; i < snapshot.Length; i++)
            {
                if (snapshot[i].Order < snapshot[i - 1].Order)
                {
                    Array.Sort(snapshot, static (a, b) => a.Order.CompareTo(b.Order));
                    break;
                }
            }
            return snapshot;
        }
    }
}

