// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.TestHooks.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;

namespace StateReplay
{
    public partial class StateReplayPlugin
    {
        // Test hook to exercise replay without console command wiring.
        public void LoadForTest(NeoSystem system) => OnSystemLoaded(system);
        public void ReplayForTest(string filePath, uint? heightOverride = null) => ReplayBlockState(filePath, heightOverride);
        public void ReplayBinaryForTest(string filePath) => ReplayBlockStateBinary(filePath);
    }
}

