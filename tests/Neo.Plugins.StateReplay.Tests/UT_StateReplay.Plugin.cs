// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StateReplay.Plugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StateReplay.Tests
{
    public partial class UT_StateReplay
    {
        [TestMethod]
        public void Plugin_HasCorrectDescription()
        {
            Assert.IsTrue(_plugin.Description.Contains("Replay"));
        }

        [TestMethod]
        public void Plugin_ConfigFileHasCorrectPath()
        {
            Assert.IsTrue(_plugin.ConfigFile.EndsWith("StateReplay.json"));
        }
    }
}

