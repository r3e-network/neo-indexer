// Copyright (C) 2015-2025 The Neo Project.
//
// UT_Notary.Helpers.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Numerics;

namespace Neo.UnitTests.SmartContract.Native
{
    public partial class UT_Notary
    {
        internal static BigInteger Call_BalanceOf(DataCache snapshot, byte[] address, Block persistingBlock)
        {
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, persistingBlock, settings: TestProtocolSettings.Default);

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Notary.Hash, "balanceOf", address);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);

            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Integer));

            return result.GetInteger();
        }

        internal static BigInteger Call_ExpirationOf(DataCache snapshot, byte[] address, Block persistingBlock)
        {
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, persistingBlock, settings: TestProtocolSettings.Default);

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Notary.Hash, "expirationOf", address);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);

            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Integer));

            return result.GetInteger();
        }

        internal static bool Call_LockDepositUntil(DataCache snapshot, byte[] address, uint till, Block persistingBlock)
        {
            using var engine = ApplicationEngine.Create(TriggerType.Application, new Transaction() { Signers = new Signer[] { new Signer() { Account = new UInt160(address), Scopes = WitnessScope.Global } }, Attributes = System.Array.Empty<TransactionAttribute>() }, snapshot, persistingBlock, settings: TestProtocolSettings.Default);

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Notary.Hash, "lockDepositUntil", address, till);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);

            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));

            return result.GetBoolean();
        }

        internal static bool Call_Withdraw(DataCache snapshot, byte[] from, byte[] to, Block persistingBlock, bool witnessedByFrom = true)
        {
            var accFrom = UInt160.Zero;
            if (witnessedByFrom)
            {
                accFrom = new UInt160(from);
            }
            using var engine = ApplicationEngine.Create(TriggerType.Application, new Transaction() { Signers = new Signer[] { new Signer() { Account = accFrom, Scopes = WitnessScope.Global } }, Attributes = System.Array.Empty<TransactionAttribute>() }, snapshot, persistingBlock, settings: TestProtocolSettings.Default);

            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.Notary.Hash, "withdraw", from, to);
            engine.LoadScript(script.ToArray());

            if (engine.Execute() != VMState.HALT)
            {
                throw engine.FaultException;
            }

            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));

            return result.GetBoolean();
        }

        internal static StorageKey CreateStorageKey(byte prefix, uint key)
        {
            return CreateStorageKey(prefix, BitConverter.GetBytes(key));
        }

        internal static StorageKey CreateStorageKey(byte prefix, byte[] key = null)
        {
            byte[] buffer = GC.AllocateUninitializedArray<byte>(sizeof(byte) + (key?.Length ?? 0));
            buffer[0] = prefix;
            key?.CopyTo(buffer.AsSpan(1));
            return new()
            {
                Id = NativeContract.GAS.Id,
                Key = buffer
            };
        }
    }
}
