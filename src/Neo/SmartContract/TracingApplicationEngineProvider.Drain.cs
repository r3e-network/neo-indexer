// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngineProvider.Drain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.SmartContract
{
    public sealed partial class TracingApplicationEngineProvider
    {
        /// <summary>
        /// Drains and returns all transaction recorders captured for a given block.
        /// </summary>
        public IReadOnlyCollection<ExecutionTraceRecorder> DrainBlock(uint blockIndex)
        {
            if (_blockRecorders.TryRemove(blockIndex, out var blockRecorder))
            {
                return blockRecorder.GetTxRecorders().Values.ToArray();
            }

            return Array.Empty<ExecutionTraceRecorder>();
        }

        /// <summary>
        /// Drains and returns the state read recorder for a block, if present.
        /// </summary>
        public BlockReadRecorder? DrainReadRecorder(uint blockIndex)
        {
            if (_readScopes.TryRemove(blockIndex, out var scope))
            {
                var recorder = scope.Recorder;
                scope.Dispose();
                return recorder;
            }

            return null;
        }
    }
}

