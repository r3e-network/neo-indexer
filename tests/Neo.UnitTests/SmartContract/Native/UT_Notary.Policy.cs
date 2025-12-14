// Copyright (C) 2015-2025 The Neo Project.
//
// UT_Notary.Policy.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.UnitTests.Extensions;
using Neo.VM;

namespace Neo.UnitTests.SmartContract.Native
{
    public partial class UT_Notary
    {
        [TestMethod]
        public void Check_GetMaxNotValidBeforeDelta()
        {
            const int defaultMaxNotValidBeforeDelta = 140;
            NativeContract.Notary.GetMaxNotValidBeforeDelta(_snapshot).Should().Be(defaultMaxNotValidBeforeDelta);
        }

        [TestMethod]
        public void Check_SetMaxNotValidBeforeDelta()
        {
            var snapshot = _snapshot.CloneCache();
            var persistingBlock = new Block { Header = new Header { Index = 1000 } };
            UInt160 committeeAddress = NativeContract.NEO.GetCommitteeAddress(snapshot);

            using var engine = ApplicationEngine.Create(TriggerType.Application, new Nep17NativeContractExtensions.ManualWitness(committeeAddress), snapshot, persistingBlock, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Notary.Hash, "setMaxNotValidBeforeDelta", 100);
            engine.LoadScript(script.ToArray());
            VMState vMState = engine.Execute();
            vMState.Should().Be(VMState.HALT);
            NativeContract.Notary.GetMaxNotValidBeforeDelta(snapshot).Should().Be(100);
        }
    }
}
