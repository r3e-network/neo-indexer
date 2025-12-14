// Copyright (C) 2015-2025 The Neo Project.
//
// UT_Notary.Deposits.BalanceAndWithdraw.cs file belongs to the neo project and is free
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
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.UnitTests.Extensions;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Neo.UnitTests.SmartContract.Native
{
    public partial class UT_Notary
    {
        [TestMethod]
        public void Check_BalanceOf()
        {
            var snapshot = _snapshot.CloneCache();
            var persistingBlock = new Block { Header = new Header { Index = 1000 } };
            UInt160 fromAddr = Contract.GetBFTAddress(TestProtocolSettings.Default.StandbyValidators);
            byte[] from = fromAddr.ToArray();
            byte[] ntr = NativeContract.Notary.Hash.ToArray();

            // Set proper current index for deposit expiration.
            var storageKey = new KeyBuilder(NativeContract.Ledger.Id, 12);
            snapshot.Add(storageKey, new StorageItem(new HashIndexState { Hash = UInt256.Zero, Index = persistingBlock.Index - 1 }));

            // Ensure that default deposit is 0.
            Call_BalanceOf(snapshot, from, persistingBlock).Should().Be(0);

            // Make initial deposit.
            var till = persistingBlock.Index + 123;
            var deposit1 = 2 * 1_0000_0000;
            var data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Any }, new ContractParameter { Type = ContractParameterType.Integer, Value = till } } };
            Assert.IsTrue(NativeContract.GAS.TransferWithTransaction(snapshot, from, ntr, deposit1, true, persistingBlock, data));

            // Ensure value is deposited.
            Call_BalanceOf(snapshot, from, persistingBlock).Should().Be(deposit1);

            // Make one more deposit with updated 'till' parameter.
            var deposit2 = 5;
            data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Any }, new ContractParameter { Type = ContractParameterType.Integer, Value = till } } };
            Assert.IsTrue(NativeContract.GAS.TransferWithTransaction(snapshot, from, ntr, deposit2, true, persistingBlock, data));

            // Ensure deposit's 'till' value is properly updated.
            Call_BalanceOf(snapshot, from, persistingBlock).Should().Be(deposit1 + deposit2);

            // Make deposit to some side account.
            UInt160 to = UInt160.Parse("01ff00ff00ff00ff00ff00ff00ff00ff00ff00a4");
            data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Hash160, Value = to }, new ContractParameter { Type = ContractParameterType.Integer, Value = till } } };
            Assert.IsTrue(NativeContract.GAS.TransferWithTransaction(snapshot, from, ntr, deposit1, true, persistingBlock, data));

            Call_BalanceOf(snapshot, to.ToArray(), persistingBlock).Should().Be(deposit1);

            // Process some Notary transaction and check that some deposited funds have been withdrawn.
            var tx1 = TestUtils.GetTransaction(NativeContract.Notary.Hash, fromAddr);
            tx1.Attributes = new TransactionAttribute[] { new NotaryAssisted() { NKeys = 4 } };
            tx1.NetworkFee = 1_0000_0000;

            // Build block to check transaction fee distribution during Gas OnPersist.
            persistingBlock = new Block
            {
                Header = new Header
                {
                    Index = (uint)TestProtocolSettings.Default.CommitteeMembersCount,
                    MerkleRoot = UInt256.Zero,
                    NextConsensus = UInt160.Zero,
                    PrevHash = UInt256.Zero,
                    Witness = new Witness() { InvocationScript = Array.Empty<byte>(), VerificationScript = Array.Empty<byte>() }
                },
                Transactions = new Transaction[] { tx1 }
            };
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
                    new ContractParameter(ContractParameterType.ByteArray){Value = key1.PublicKey.ToArray()},
                }
                }
            );
            snapshot.Commit();

            // Execute OnPersist script.
            var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
            var engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, persistingBlock, settings: TestProtocolSettings.Default);
            engine.LoadScript(script.ToArray());
            Assert.IsTrue(engine.Execute() == VMState.HALT);
            snapshot.Commit();

            // Check that transaction's fees were paid by from's deposit.
            Call_BalanceOf(snapshot, from, persistingBlock).Should().Be(deposit1 + deposit2 - tx1.NetworkFee - tx1.SystemFee);

            // Withdraw own deposit.
            persistingBlock.Header.Index = till + 1;
            var currentBlock = snapshot.GetAndChange(storageKey, () => new StorageItem(new HashIndexState()));
            currentBlock.GetInteroperable<HashIndexState>().Index = till + 1;
            Call_Withdraw(snapshot, from, from, persistingBlock);

            // Check that no deposit is left.
            Call_BalanceOf(snapshot, from, persistingBlock).Should().Be(0);
        }

        [TestMethod]
        public void Check_Withdraw()
        {
            var snapshot = _snapshot.CloneCache();
            var persistingBlock = new Block { Header = new Header { Index = 1000 } };
            UInt160 fromAddr = Contract.GetBFTAddress(TestProtocolSettings.Default.StandbyValidators);
            byte[] from = fromAddr.ToArray();

            // Set proper current index to get proper deposit expiration height.
            var storageKey = new KeyBuilder(NativeContract.Ledger.Id, 12);
            snapshot.Add(storageKey, new StorageItem(new HashIndexState { Hash = UInt256.Zero, Index = persistingBlock.Index - 1 }));

            // Ensure that default deposit is 0.
            Call_BalanceOf(snapshot, from, persistingBlock).Should().Be(0);

            // Make initial deposit.
            var till = persistingBlock.Index + 123;
            var deposit1 = 2 * 1_0000_0000;
            var data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Any }, new ContractParameter { Type = ContractParameterType.Integer, Value = till } } };
            Assert.IsTrue(NativeContract.GAS.TransferWithTransaction(snapshot, from, NativeContract.Notary.Hash.ToArray(), deposit1, true, persistingBlock, data));

            // Ensure value is deposited.
            Call_BalanceOf(snapshot, from, persistingBlock).Should().Be(deposit1);

            // Unwitnessed withdraw should fail.
            UInt160 sideAccount = UInt160.Parse("01ff00ff00ff00ff00ff00ff00ff00ff00ff00a4");
            Call_Withdraw(snapshot, from, sideAccount.ToArray(), persistingBlock, false).Should().Be(false);

            // Withdraw missing (zero) deposit should fail.
            Call_Withdraw(snapshot, sideAccount.ToArray(), sideAccount.ToArray(), persistingBlock).Should().Be(false);

            // Withdraw before deposit expiration should fail.
            Call_Withdraw(snapshot, from, from, persistingBlock).Should().Be(false);

            // Good.
            persistingBlock.Header.Index = till + 1;
            var currentBlock = snapshot.GetAndChange(storageKey, () => new StorageItem(new HashIndexState()));
            currentBlock.GetInteroperable<HashIndexState>().Index = till + 1;
            Call_Withdraw(snapshot, from, from, persistingBlock).Should().Be(true);

            // Check that no deposit is left.
            Call_BalanceOf(snapshot, from, persistingBlock).Should().Be(0);
        }
    }
}

