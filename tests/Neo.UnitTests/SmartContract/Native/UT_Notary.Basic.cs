// Copyright (C) 2015-2025 The Neo Project.
//
// UT_Notary.Basic.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.SmartContract.Native;

namespace Neo.UnitTests.SmartContract.Native
{
    public partial class UT_Notary
    {
        [TestMethod]
        public void Check_Name() => NativeContract.Notary.Name.Should().Be(nameof(Notary));
    }
}

