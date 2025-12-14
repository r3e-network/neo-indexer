// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Core.Modes.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static bool IsBinaryMode(StateRecorderSettings.UploadMode mode)
            => mode is StateRecorderSettings.UploadMode.Binary or StateRecorderSettings.UploadMode.Both;

        private static bool IsRestApiMode(StateRecorderSettings.UploadMode mode)
            => mode is StateRecorderSettings.UploadMode.RestApi
                or StateRecorderSettings.UploadMode.Postgres
                or StateRecorderSettings.UploadMode.Both;
    }
}

