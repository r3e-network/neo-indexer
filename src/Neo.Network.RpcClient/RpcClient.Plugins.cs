// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.Plugins.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using System;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public partial class RpcClient
    {
        #region Plugins

        /// <summary>
        /// Returns the contract log based on the specified txHash. The complete contract logs are stored under the ApplicationLogs directory.
        /// This method is provided by the plugin ApplicationLogs.
        /// </summary>
        public async Task<RpcApplicationLog> GetApplicationLogAsync(string txHash)
        {
            var result = await RpcSendAsync(GetRpcName(), txHash).ConfigureAwait(false);
            return RpcApplicationLog.FromJson((JObject)result, protocolSettings);
        }

        /// <summary>
        /// Returns the contract log based on the specified txHash. The complete contract logs are stored under the ApplicationLogs directory.
        /// This method is provided by the plugin ApplicationLogs.
        /// </summary>
        public async Task<RpcApplicationLog> GetApplicationLogAsync(string txHash, TriggerType triggerType)
        {
            var result = await RpcSendAsync(GetRpcName(), txHash, triggerType).ConfigureAwait(false);
            return RpcApplicationLog.FromJson((JObject)result, protocolSettings);
        }

        /// <summary>
        /// Returns all the NEP-17 transaction information occurred in the specified address.
        /// This method is provided by the plugin RpcNep17Tracker.
        /// </summary>
        /// <param name="address">The address to query the transaction information.</param>
        /// <param name="startTimestamp">The start block Timestamp, default to seven days before UtcNow</param>
        /// <param name="endTimestamp">The end block Timestamp, default to UtcNow</param>
        public async Task<RpcNep17Transfers> GetNep17TransfersAsync(string address, ulong? startTimestamp = default, ulong? endTimestamp = default)
        {
            startTimestamp ??= 0;
            endTimestamp ??= DateTime.UtcNow.ToTimestampMS();
            var result = await RpcSendAsync(GetRpcName(), address.AsScriptHash(), startTimestamp, endTimestamp)
                .ConfigureAwait(false);
            return RpcNep17Transfers.FromJson((JObject)result, protocolSettings);
        }

        /// <summary>
        /// Returns the balance of all NEP-17 assets in the specified address.
        /// This method is provided by the plugin RpcNep17Tracker.
        /// </summary>
        public async Task<RpcNep17Balances> GetNep17BalancesAsync(string address)
        {
            var result = await RpcSendAsync(GetRpcName(), address.AsScriptHash())
                .ConfigureAwait(false);
            return RpcNep17Balances.FromJson((JObject)result, protocolSettings);
        }

        #endregion Plugins
    }
}
