// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.ReorgGuard.ExecuteIfCanonical.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static async Task ExecuteIfCanonicalAsync(
            uint blockIndex,
            string expectedBlockHash,
            string description,
            Func<Task> work)
        {
            if (string.IsNullOrWhiteSpace(expectedBlockHash))
            {
                await work().ConfigureAwait(false);
                return;
            }

            if (TryGetCanonicalBlockHash(blockIndex, out var canonical) &&
                !string.Equals(canonical, expectedBlockHash, StringComparison.Ordinal))
            {
                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                    $"Skipping {description} for block {blockIndex}: block hash no longer canonical.");
                return;
            }

            await WaitForReorgCleanupAsync(blockIndex, expectedBlockHash).ConfigureAwait(false);

            if (TryGetCanonicalBlockHash(blockIndex, out canonical) &&
                !string.Equals(canonical, expectedBlockHash, StringComparison.Ordinal))
            {
                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Debug,
                    $"Skipping {description} for block {blockIndex}: block hash changed during reorg cleanup.");
                return;
            }

            await work().ConfigureAwait(false);
        }
    }
}

