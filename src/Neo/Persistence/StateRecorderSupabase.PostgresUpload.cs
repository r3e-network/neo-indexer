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

using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
#if !NET9_0_OR_GREATER
        private static Task UploadPostgresAsync(BlockReadRecorder recorder, StateRecorderSettings settings)
        {
            Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                "PostgreSQL direct upload requires net9.0 or greater.");
            return Task.CompletedTask;
        }
#endif
    }
}
