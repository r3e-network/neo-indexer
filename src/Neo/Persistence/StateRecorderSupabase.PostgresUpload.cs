// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.PostgresUpload.cs file belongs to the neo project and is free
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
	        #region PostgreSQL Direct Upload

#if !NET9_0_OR_GREATER
	        private static Task UploadPostgresAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
	        {
	            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
	                "PostgreSQL direct upload requires net9.0 or greater.");
	            return Task.CompletedTask;
	        }
#endif

	        #endregion
	}
}
