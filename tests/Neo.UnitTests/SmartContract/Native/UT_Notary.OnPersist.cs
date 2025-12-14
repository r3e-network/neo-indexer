// Copyright (C) 2015-2025 The Neo Project.
//
// UT_Notary.OnPersist.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.UnitTests.Extensions;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;

namespace Neo.UnitTests.SmartContract.Native
{
    public partial class UT_Notary
    {
        [TestMethod]
        public void Check_OnPersist_FeePerKeyUpdate()
        {
            // Hardcode test values.
            const uint defaultNotaryAssistedFeePerKey = 1000_0000;
            const uint newNotaryAssistedFeePerKey = 5000_0000;
            const byte NKeys = 4;

            // Generate one transaction with NotaryAssisted attribute with hardcoded NKeys values.
            var from = Contract.GetBFTAddress(TestProtocolSettings.Default.StandbyValidators);
            var tx2 = TestUtils.GetTransaction(from);
            tx2.Attributes = new TransactionAttribute[] { new NotaryAssisted() { NKeys = NKeys } };
            var netFee = 1_0000_0000; // enough to cover defaultNotaryAssistedFeePerKey, but not enough to cover newNotaryAssistedFeePerKey.
            tx2.NetworkFee = netFee;
            tx2.SystemFee = 1000_0000;

            // Calculate overall expected Notary nodes reward.
            var expectedNotaryReward = (NKeys + 1) * defaultNotaryAssistedFeePerKey;

            // Build block to check transaction fee distribution during Gas OnPersist.
            var persistingBlock = new Block
            {
                Header = new Header
                {
                    Index = 10,
                    MerkleRoot = UInt256.Zero,
                    NextConsensus = UInt160.Zero,
                    PrevHash = UInt256.Zero,
                    Witness = new Witness() { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() }
                },
                Transactions = new Transaction[] { tx2 }
            };
            var snapshot = _snapshot.CloneCache();

            // Designate Notary node.
            byte[] privateKey1 = new byte[32];
            var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(privateKey1);
            KeyPair key1 = new KeyPair(privateKey1);
            UInt160 committeeMultiSigAddr = NativeContract.NEO.GetCommitteeAddress(snapshot);
            var ret = NativeContract.RoleManagement.Call(
                snapshot,
                new Nep17NativeContractExtensions.ManualWitness(committeeMultiSigAddr),
                new Block { Header = new Header() },
                "designateAsRole",
                new ContractParameter(ContractParameterType.Integer) { Value = new BigInteger((int)Role.P2PNotary) },
                new ContractParameter(ContractParameterType.Array)
                {
                    Value = new List<ContractParameter>(){
                    new ContractParameter(ContractParameterType.ByteArray){Value = key1.PublicKey.ToArray()}
                }
                }
            );
            snapshot.Commit();

            // Create engine with custom settings (HF_Echidna should be enabled to properly interact with NotaryAssisted attribute).
            var settings = ProtocolSettings.Default with
            {
                Network = 0x334F454Eu,
                StandbyCommittee =
                [
                ECPoint.Parse("03b209fd4f53a7170ea4444e0cb0a6bb6a53c2bd016926989cf85f9b0fba17a70c", ECCurve.Secp256r1),
                ECPoint.Parse("02df48f60e8f3e01c48ff40b9b7f1310d7a8b2a193188befe1c2e3df740e895093", ECCurve.Secp256r1),
                ECPoint.Parse("03b8d9d5771d8f513aa0869b9cc8d50986403b78c6da36890638c3d46a5adce04a", ECCurve.Secp256r1),
                ECPoint.Parse("02ca0e27697b9c248f6f16e085fd0061e26f44da85b58ee835c110caa5ec3ba554", ECCurve.Secp256r1),
                ECPoint.Parse("024c7b7fb6c310fccf1ba33b082519d82964ea93868d676662d4a59ad548df0e7d", ECCurve.Secp256r1),
                ECPoint.Parse("02aaec38470f6aad0042c6e877cfd8087d2676b0f516fddd362801b9bd3936399e", ECCurve.Secp256r1),
                ECPoint.Parse("02486fd15702c4490a26703112a5cc1d0923fd697a33406bd5a1c00e0013b09a70", ECCurve.Secp256r1)
                ],
                ValidatorsCount = 7,
                Hardforks = new Dictionary<Hardfork, uint>{
                    {Hardfork.HF_Aspidochelone, 1},
                    {Hardfork.HF_Basilisk, 2},
                    {Hardfork.HF_Cockatrice, 3},
                    {Hardfork.HF_Domovoi, 4},
                    {Hardfork.HF_Echidna, 5}
                }.ToImmutableDictionary()
            };

            // Imitate Blockchain's Persist behaviour: OnPersist + transactions processing.
            // Execute OnPersist firstly:
            var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
            var engine = ApplicationEngine.Create(TriggerType.OnPersist, new Nep17NativeContractExtensions.ManualWitness(committeeMultiSigAddr), snapshot, persistingBlock, settings: settings);
            engine.LoadScript(script.ToArray());
            Assert.IsTrue(engine.Execute() == VMState.HALT, engine.FaultException?.ToString());
            snapshot.Commit();

            // Process transaction that changes NotaryServiceFeePerKey after OnPersist.
            ret = NativeContract.Policy.Call(engine,
                "setAttributeFee", new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)(byte)TransactionAttributeType.NotaryAssisted }, new ContractParameter(ContractParameterType.Integer) { Value = newNotaryAssistedFeePerKey });
            Assert.IsNull(ret);
            snapshot.Commit();

            // Process tx2 with NotaryAssisted attribute.
            engine = ApplicationEngine.Create(TriggerType.Application, tx2, snapshot, persistingBlock, settings: TestProtocolSettings.Default, tx2.SystemFee);
            engine.LoadScript(tx2.Script);
            Assert.IsTrue(engine.Execute() == VMState.HALT);
            snapshot.Commit();

            // Ensure that Notary reward is distributed based on the old value of NotaryAssisted price
            // and no underflow happens during GAS distribution.
            ECPoint[] validators = NativeContract.NEO.GetNextBlockValidators(engine.SnapshotCache, engine.ProtocolSettings.ValidatorsCount);
            var primary = Contract.CreateSignatureRedeemScript(validators[engine.PersistingBlock.PrimaryIndex]).ToScriptHash();
            NativeContract.GAS.BalanceOf(snapshot, primary).Should().Be(netFee - expectedNotaryReward);
            NativeContract.GAS.BalanceOf(engine.SnapshotCache, Contract.CreateSignatureRedeemScript(key1.PublicKey).ToScriptHash()).Should().Be(expectedNotaryReward);
        }

        [TestMethod]
        public void Check_OnPersist_NotaryRewards()
        {
            // Hardcode test values.
            const uint defaultNotaryssestedFeePerKey = 1000_0000;
            const byte NKeys1 = 4;
            const byte NKeys2 = 6;

            // Generate two transactions with NotaryAssisted attributes with hardcoded NKeys values.
            var from = Contract.GetBFTAddress(TestProtocolSettings.Default.StandbyValidators);
            var tx1 = TestUtils.GetTransaction(from);
            tx1.Attributes = new TransactionAttribute[] { new NotaryAssisted() { NKeys = NKeys1 } };
            var netFee1 = 1_0000_0000;
            tx1.NetworkFee = netFee1;
            var tx2 = TestUtils.GetTransaction(from);
            tx2.Attributes = new TransactionAttribute[] { new NotaryAssisted() { NKeys = NKeys2 } };
            var netFee2 = 2_0000_0000;
            tx2.NetworkFee = netFee2;

            // Calculate overall expected Notary nodes reward.
            var expectedNotaryReward = (NKeys1 + 1) * defaultNotaryssestedFeePerKey + (NKeys2 + 1) * defaultNotaryssestedFeePerKey;

            // Build block to check transaction fee distribution during Gas OnPersist.
            var persistingBlock = new Block
            {
                Header = new Header
                {
                    Index = (uint)TestProtocolSettings.Default.CommitteeMembersCount,
                    MerkleRoot = UInt256.Zero,
                    NextConsensus = UInt160.Zero,
                    PrevHash = UInt256.Zero,
                    Witness = new Witness() { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() }
                },
                Transactions = new Transaction[] { tx1, tx2 }
            };
            var snapshot = _snapshot.CloneCache();

            // Designate several Notary nodes.
            byte[] privateKey1 = new byte[32];
            var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(privateKey1);
            KeyPair key1 = new KeyPair(privateKey1);
            byte[] privateKey2 = new byte[32];
            rng.GetBytes(privateKey2);
            KeyPair key2 = new KeyPair(privateKey2);
            UInt160 committeeMultiSigAddr = NativeContract.NEO.GetCommitteeAddress(snapshot);
            var ret = NativeContract.RoleManagement.Call(
                snapshot,
                new Nep17NativeContractExtensions.ManualWitness(committeeMultiSigAddr),
                new Block { Header = new Header() },
                "designateAsRole",
                new ContractParameter(ContractParameterType.Integer) { Value = new BigInteger((int)Role.P2PNotary) },
                new ContractParameter(ContractParameterType.Array)
                {
                    Value = new List<ContractParameter>(){
                    new ContractParameter(ContractParameterType.ByteArray){Value = key1.PublicKey.ToArray()},
                    new ContractParameter(ContractParameterType.ByteArray){Value = key2.PublicKey.ToArray()},
                }
                }
            );
            snapshot.Commit();

            var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
            var engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, persistingBlock, settings: TestProtocolSettings.Default);

            // Check that block's Primary balance is 0.
            ECPoint[] validators = NativeContract.NEO.GetNextBlockValidators(engine.SnapshotCache, engine.ProtocolSettings.ValidatorsCount);
            var primary = Contract.CreateSignatureRedeemScript(validators[engine.PersistingBlock.PrimaryIndex]).ToScriptHash();
            NativeContract.GAS.BalanceOf(engine.SnapshotCache, primary).Should().Be(0);

            // Execute OnPersist script.
            engine.LoadScript(script.ToArray());
            Assert.IsTrue(engine.Execute() == VMState.HALT);

            // Check that proper amount of GAS was minted to block's Primary and the rest
            // is evenly devided between designated Notary nodes as a reward.
            Assert.AreEqual(2 + 1 + 2, engine.Notifications.Count()); // burn tx1 and tx2 network fee + mint primary reward + transfer reward to Notary1 and Notary2
            Assert.AreEqual(netFee1 + netFee2 - expectedNotaryReward, engine.Notifications[2].State[2]);
            NativeContract.GAS.BalanceOf(engine.SnapshotCache, primary).Should().Be(netFee1 + netFee2 - expectedNotaryReward);
            Assert.AreEqual(expectedNotaryReward / 2, engine.Notifications[3].State[2]);
            NativeContract.GAS.BalanceOf(engine.SnapshotCache, Contract.CreateSignatureRedeemScript(key1.PublicKey).ToScriptHash()).Should().Be(expectedNotaryReward / 2);
            Assert.AreEqual(expectedNotaryReward / 2, engine.Notifications[4].State[2]);
            NativeContract.GAS.BalanceOf(engine.SnapshotCache, Contract.CreateSignatureRedeemScript(key2.PublicKey).ToScriptHash()).Should().Be(expectedNotaryReward / 2);
        }
    }
}

