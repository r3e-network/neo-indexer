// Copyright (C) 2015-2025 The Neo Project.
//
// UT_Notary.Deposits.cs file belongs to the neo project and is free
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
using System.Collections.Generic;
using System.Numerics;

namespace Neo.UnitTests.SmartContract.Native
{
    public partial class UT_Notary
    {
        [TestMethod]
        public void Check_OnNEP17Payment()
        {
            var snapshot = _snapshot.CloneCache();
            var persistingBlock = new Block { Header = new Header { Index = 1000 } };
            byte[] from = Contract.GetBFTAddress(TestProtocolSettings.Default.StandbyValidators).ToArray();
            byte[] to = NativeContract.Notary.Hash.ToArray();

            // Set proper current index for deposit's Till parameter check.
            var storageKey = new KeyBuilder(NativeContract.Ledger.Id, 12);
            snapshot.Add(storageKey, new StorageItem(new HashIndexState { Hash = UInt256.Zero, Index = persistingBlock.Index - 1 }));

            // Non-GAS transfer should fail.
            Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => NativeContract.NEO.Transfer(snapshot, from, to, BigInteger.Zero, true, persistingBlock));

            // GAS transfer with invalid data format should fail.
            Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => NativeContract.GAS.Transfer(snapshot, from, to, BigInteger.Zero, true, persistingBlock, 5));

            // GAS transfer with wrong number of data elements should fail.
            var data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Boolean, Value = true } } };
            Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => NativeContract.GAS.Transfer(snapshot, from, to, BigInteger.Zero, true, persistingBlock, data));

            // Gas transfer with invalid Till parameter should fail.
            data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Any }, new ContractParameter { Type = ContractParameterType.Integer, Value = persistingBlock.Index } } };
            Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => NativeContract.GAS.TransferWithTransaction(snapshot, from, to, BigInteger.Zero, true, persistingBlock, data));

            // Insufficient first deposit.
            data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Any }, new ContractParameter { Type = ContractParameterType.Integer, Value = persistingBlock.Index + 100 } } };
            Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => NativeContract.GAS.TransferWithTransaction(snapshot, from, to, 2 * 1000_0000 - 1, true, persistingBlock, data));

            // Good deposit.
            data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Any }, new ContractParameter { Type = ContractParameterType.Integer, Value = persistingBlock.Index + 100 } } };
            Assert.IsTrue(NativeContract.GAS.TransferWithTransaction(snapshot, from, to, 2 * 1000_0000 + 1, true, persistingBlock, data));
        }

        [TestMethod]
        public void Check_ExpirationOf()
        {
            var snapshot = _snapshot.CloneCache();
            var persistingBlock = new Block { Header = new Header { Index = 1000 } };
            byte[] from = Contract.GetBFTAddress(TestProtocolSettings.Default.StandbyValidators).ToArray();
            byte[] ntr = NativeContract.Notary.Hash.ToArray();

            // Set proper current index for deposit's Till parameter check.
            var storageKey = new KeyBuilder(NativeContract.Ledger.Id, 12);
            snapshot.Add(storageKey, new StorageItem(new HashIndexState { Hash = UInt256.Zero, Index = persistingBlock.Index - 1 }));

            // Check that 'till' of an empty deposit is 0 by default.
            Call_ExpirationOf(snapshot, from, persistingBlock).Should().Be(0);

            // Make initial deposit.
            var till = persistingBlock.Index + 123;
            var data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Any }, new ContractParameter { Type = ContractParameterType.Integer, Value = till } } };
            Assert.IsTrue(NativeContract.GAS.TransferWithTransaction(snapshot, from, ntr, 2 * 1000_0000 + 1, true, persistingBlock, data));

            // Ensure deposit's 'till' value is properly set.
            Call_ExpirationOf(snapshot, from, persistingBlock).Should().Be(till);

            // Make one more deposit with updated 'till' parameter.
            till += 5;
            data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Any }, new ContractParameter { Type = ContractParameterType.Integer, Value = till } } };
            Assert.IsTrue(NativeContract.GAS.TransferWithTransaction(snapshot, from, ntr, 5, true, persistingBlock, data));

            // Ensure deposit's 'till' value is properly updated.
            Call_ExpirationOf(snapshot, from, persistingBlock).Should().Be(till);

            // Make deposit to some side account with custom 'till' value.
            UInt160 to = UInt160.Parse("01ff00ff00ff00ff00ff00ff00ff00ff00ff00a4");
            data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Hash160, Value = to }, new ContractParameter { Type = ContractParameterType.Integer, Value = till } } };
            Assert.IsTrue(NativeContract.GAS.TransferWithTransaction(snapshot, from, ntr, 2 * 1000_0000 + 1, true, persistingBlock, data));

            // Default 'till' value should be set for to's deposit.
            var defaultDeltaTill = 5760;
            Call_ExpirationOf(snapshot, to.ToArray(), persistingBlock).Should().Be(persistingBlock.Index - 1 + defaultDeltaTill);

            // Withdraw own deposit.
            persistingBlock.Header.Index = till + 1;
            var currentBlock = snapshot.GetAndChange(storageKey, () => new StorageItem(new HashIndexState()));
            currentBlock.GetInteroperable<HashIndexState>().Index = till + 1;
            Call_Withdraw(snapshot, from, from, persistingBlock);

            // Check that 'till' value is properly updated.
            Call_ExpirationOf(snapshot, from, persistingBlock).Should().Be(0);
        }

        [TestMethod]
        public void Check_LockDepositUntil()
        {
            var snapshot = _snapshot.CloneCache();
            var persistingBlock = new Block { Header = new Header { Index = 1000 } };
            byte[] from = Contract.GetBFTAddress(TestProtocolSettings.Default.StandbyValidators).ToArray();

            // Set proper current index for deposit's Till parameter check.
            var storageKey = new KeyBuilder(NativeContract.Ledger.Id, 12);
            snapshot.Add(storageKey, new StorageItem(new HashIndexState { Hash = UInt256.Zero, Index = persistingBlock.Index - 1 }));

            // Check that 'till' of an empty deposit is 0 by default.
            Call_ExpirationOf(snapshot, from, persistingBlock).Should().Be(0);

            // Update `till` value of an empty deposit should fail.
            Call_LockDepositUntil(snapshot, from, 123, persistingBlock).Should().Be(false);
            Call_ExpirationOf(snapshot, from, persistingBlock).Should().Be(0);

            // Make initial deposit.
            var till = persistingBlock.Index + 123;
            var data = new ContractParameter { Type = ContractParameterType.Array, Value = new List<ContractParameter>() { new ContractParameter { Type = ContractParameterType.Any }, new ContractParameter { Type = ContractParameterType.Integer, Value = till } } };
            Assert.IsTrue(NativeContract.GAS.TransferWithTransaction(snapshot, from, NativeContract.Notary.Hash.ToArray(), 2 * 1000_0000 + 1, true, persistingBlock, data));

            // Ensure deposit's 'till' value is properly set.
            Call_ExpirationOf(snapshot, from, persistingBlock).Should().Be(till);

            // Update deposit's `till` value for side account should fail.
            UInt160 other = UInt160.Parse("01ff00ff00ff00ff00ff00ff00ff00ff00ff00a4");
            Call_LockDepositUntil(snapshot, other.ToArray(), till + 10, persistingBlock).Should().Be(false);
            Call_ExpirationOf(snapshot, from, persistingBlock).Should().Be(till);

            // Decrease deposit's `till` value should fail.
            Call_LockDepositUntil(snapshot, from, till - 1, persistingBlock).Should().Be(false);
            Call_ExpirationOf(snapshot, from, persistingBlock).Should().Be(till);

            // Good.
            till += 10;
            Call_LockDepositUntil(snapshot, from, till, persistingBlock).Should().Be(true);
            Call_ExpirationOf(snapshot, from, persistingBlock).Should().Be(till);
        }
    }
}

