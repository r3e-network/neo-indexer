// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Settings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Extensions;
using Neo.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
using NpgsqlTypes;
#endif


namespace Neo.Persistence
{
	public static partial class StateRecorderSupabase
	{
		#region Environment
	        private static int GetUploadQueueWorkers()
	        {
	            var raw = Environment.GetEnvironmentVariable(UploadQueueWorkersEnvVar);
	            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
	                return parsed;
	            return TraceUploadConcurrency;
	        }

        private static int GetPositiveEnvIntOrDefault(string envVar, int defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
                return parsed;
            return defaultValue;
        }

        private static int GetTraceUploadBatchSize()
        {
            var raw = Environment.GetEnvironmentVariable(TraceBatchSizeEnvVar);
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
            {
                return Math.Min(parsed, MaxTraceBatchSize);
            }
            return DefaultTraceBatchSize;
        }

	        private static int GetTraceUploadConcurrency()
	        {
	            var raw = Environment.GetEnvironmentVariable(TraceUploadConcurrencyEnvVar);
	            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed) && parsed > 0)
	                return parsed;
	            return 4;
	        }

	        private static int GetLowPriorityTraceLaneConcurrency()
	        {
	            // Reserve at least one global upload slot for high-priority uploads.
	            // When concurrency is 1, there is nothing to reserve; traces must use the only slot.
	            return TraceUploadConcurrency <= 1 ? 1 : TraceUploadConcurrency - 1;
	        }
		#endregion
	}
}
