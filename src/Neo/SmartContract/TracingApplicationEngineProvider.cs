// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngineProvider.cs file belongs to the neo project and is free
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
using System.Collections.Concurrent;

namespace Neo.SmartContract
{
    /// <summary>
    /// Factory for creating <see cref="TracingApplicationEngine"/> instances that share the same tracing configuration.
    /// </summary>
    public sealed partial class TracingApplicationEngineProvider : IApplicationEngineProvider
    {
        private readonly Func<ExecutionTraceRecorder> _recorderFactory;
        private readonly ExecutionTraceLevel _traceLevel;
        private readonly ConcurrentDictionary<uint, BlockTraceRecorder> _blockRecorders = new();
        private readonly ConcurrentDictionary<uint, BlockReadRecorderScope> _readScopes = new();

        /// <summary>
        /// Initializes a new provider with an optional recorder factory and trace level.
        /// </summary>
        public TracingApplicationEngineProvider(
            Func<ExecutionTraceRecorder>? recorderFactory = null,
            ExecutionTraceLevel traceLevel = ExecutionTraceLevel.All)
        {
            _recorderFactory = recorderFactory ?? (() => new ExecutionTraceRecorder());
            _traceLevel = traceLevel;
        }

        /// <summary>
        /// Initializes a new provider using a fixed recorder instance.
        /// </summary>
        public TracingApplicationEngineProvider(
            ExecutionTraceRecorder recorder,
            ExecutionTraceLevel traceLevel = ExecutionTraceLevel.All)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));
            _recorderFactory = () => recorder;
            _traceLevel = traceLevel;
        }
    }
}

