// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexerPlugin.Handlers.Committed.UploadModes.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class BlockStateIndexerPlugin
    {
        private static (bool AllowBinaryUploads, bool AllowDatabaseUploads) ResolveUploadAllows(StateRecorderSettings recorderSettings)
        {
            var pluginMode = Settings.Default.UploadMode;

            var pluginAllowsBinary = pluginMode is StateRecorderSettings.UploadMode.Binary or StateRecorderSettings.UploadMode.Both;
            var pluginAllowsDatabase = pluginMode is StateRecorderSettings.UploadMode.RestApi
                or StateRecorderSettings.UploadMode.Postgres
                or StateRecorderSettings.UploadMode.Both;

            var envAllowsBinary = recorderSettings.Mode is StateRecorderSettings.UploadMode.Binary or StateRecorderSettings.UploadMode.Both;
            var envAllowsDatabase = recorderSettings.Mode is StateRecorderSettings.UploadMode.RestApi
                or StateRecorderSettings.UploadMode.Postgres
                or StateRecorderSettings.UploadMode.Both;

            var allowBinaryUploads = pluginAllowsBinary && envAllowsBinary;
            var allowDatabaseUploads = pluginAllowsDatabase && envAllowsDatabase;

            return (allowBinaryUploads, allowDatabaseUploads);
        }
    }
}
