// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Node.Relay.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.Extensions;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Processes the result of a transaction or block relay and returns appropriate response or throws an exception.
        /// </summary>
        /// <param name="reason">The verification result of the relay.</param>
        /// <param name="hash">The hash of the transaction or block.</param>
        /// <returns>A JObject containing the hash if successful, otherwise throws an RpcException.</returns>
        private static JObject GetRelayResult(VerifyResult reason, UInt256 hash)
        {
            switch (reason)
            {
                case VerifyResult.Succeed:
                    {
                        var ret = new JObject();
                        ret["hash"] = hash.ToString();
                        return ret;
                    }
                case VerifyResult.AlreadyExists:
                    {
                        throw new RpcException(RpcError.AlreadyExists.WithData(reason.ToString()));
                    }
                case VerifyResult.AlreadyInPool:
                    {
                        throw new RpcException(RpcError.AlreadyInPool.WithData(reason.ToString()));
                    }
                case VerifyResult.OutOfMemory:
                    {
                        throw new RpcException(RpcError.MempoolCapReached.WithData(reason.ToString()));
                    }
                case VerifyResult.InvalidScript:
                    {
                        throw new RpcException(RpcError.InvalidScript.WithData(reason.ToString()));
                    }
                case VerifyResult.InvalidAttribute:
                    {
                        throw new RpcException(RpcError.InvalidAttribute.WithData(reason.ToString()));
                    }
                case VerifyResult.InvalidSignature:
                    {
                        throw new RpcException(RpcError.InvalidSignature.WithData(reason.ToString()));
                    }
                case VerifyResult.OverSize:
                    {
                        throw new RpcException(RpcError.InvalidSize.WithData(reason.ToString()));
                    }
                case VerifyResult.Expired:
                    {
                        throw new RpcException(RpcError.ExpiredTransaction.WithData(reason.ToString()));
                    }
                case VerifyResult.InsufficientFunds:
                    {
                        throw new RpcException(RpcError.InsufficientFunds.WithData(reason.ToString()));
                    }
                case VerifyResult.PolicyFail:
                    {
                        throw new RpcException(RpcError.PolicyFailed.WithData(reason.ToString()));
                    }
                default:
                    {
                        throw new RpcException(RpcError.VerificationFailed.WithData(reason.ToString()));
                    }
            }
        }

        /// <summary>
        /// Sends a raw transaction to the network.
        /// </summary>
        /// <param name="base64Tx">The base64-encoded transaction.</param>
        /// <returns>A JToken containing the result of the transaction relay.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken SendRawTransaction(string base64Tx)
        {
            Transaction tx = Result.Ok_Or(() => Convert.FromBase64String(base64Tx).AsSerializable<Transaction>(), RpcError.InvalidParams.WithData($"Invalid Transaction Format: {base64Tx}"));
            RelayResult reason = system.Blockchain.Ask<RelayResult>(tx).Result;
            return GetRelayResult(reason.Result, tx.Hash);
        }

        /// <summary>
        /// Submits a new block to the network.
        /// </summary>
        /// <param name="base64Block">The base64-encoded block.</param>
        /// <returns>A JToken containing the result of the block submission.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken SubmitBlock(string base64Block)
        {
            Block block = Result.Ok_Or(() => Convert.FromBase64String(base64Block).AsSerializable<Block>(), RpcError.InvalidParams.WithData($"Invalid Block Format: {base64Block}"));
            RelayResult reason = system.Blockchain.Ask<RelayResult>(block).Result;
            return GetRelayResult(reason.Result, block.Hash);
        }
    }
}
