// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngine.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

	using Neo.Network.P2P.Payloads;
	using Neo.Persistence;
	using Neo.VM;
	using System;
	using System.Collections.Generic;

namespace Neo.SmartContract
{
	    /// <summary>
	    /// ApplicationEngine implementation that emits extended traces for syscalls, storage mutations, and notifications.
	    /// </summary>
	    public partial class TracingApplicationEngine : ApplicationEngine
	    {
	        private readonly ExecutionTraceRecorder _traceRecorder;
	        private readonly ExecutionTraceLevel _traceLevel;
	        private readonly Dictionary<int, UInt160> _contractHashCache = new();

        /// <summary>
        /// Gets the recorder used to capture execution traces.
        /// </summary>
        public ExecutionTraceRecorder TraceRecorder => _traceRecorder;

        /// <summary>
        /// Gets the enabled trace level flags.
        /// </summary>
        public ExecutionTraceLevel TraceLevel => _traceLevel;

        /// <summary>
        /// Initializes a new instance of <see cref="TracingApplicationEngine"/>.
        /// </summary>
        public TracingApplicationEngine(
            TriggerType trigger,
            IVerifiable container,
            DataCache snapshotCache,
            Block? persistingBlock,
            ProtocolSettings settings,
            long gas,
            ExecutionTraceRecorder traceRecorder,
            ExecutionTraceLevel traceLevel,
            IDiagnostic? diagnostic = null,
            JumpTable? jumpTable = null)
            : base(trigger, container, snapshotCache, persistingBlock, settings, gas, diagnostic, jumpTable)
        {
	            _traceRecorder = traceRecorder ?? throw new ArgumentNullException(nameof(traceRecorder));
	            _traceLevel = traceLevel;
	        }

	        private bool ShouldTrace(ExecutionTraceLevel level)
	        {
	            if (_traceRecorder is null || !_traceRecorder.IsEnabled)
                return false;
	            return (_traceLevel & level) != 0;
	        }

	        private static ReadOnlyMemory<byte> Clone(ReadOnlyMemory<byte> source)
	        {
	            return source.IsEmpty ? ReadOnlyMemory<byte>.Empty : source.ToArray();
	        }
	    }
	}
