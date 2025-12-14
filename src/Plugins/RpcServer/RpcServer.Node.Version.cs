// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Node.Version.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Network.P2P;
using System;
using System.Linq;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Gets version information about the node, including network, protocol, and RPC settings.
        /// </summary>
        /// <returns>A JObject containing detailed version and configuration information.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetVersion()
        {
            JObject json = new();
            json["tcpport"] = localNode.ListenerTcpPort;
            json["nonce"] = LocalNode.Nonce;
            json["useragent"] = LocalNode.UserAgent;
            // rpc settings
            JObject rpc = new();
            rpc["maxiteratorresultitems"] = settings.MaxIteratorResultItems;
            rpc["sessionenabled"] = settings.SessionEnabled;
            // protocol settings
            JObject protocol = new();
            protocol["addressversion"] = system.Settings.AddressVersion;
            protocol["network"] = system.Settings.Network;
            protocol["validatorscount"] = system.Settings.ValidatorsCount;
            protocol["msperblock"] = system.GetTimePerBlock().TotalMilliseconds;
            protocol["maxtraceableblocks"] = system.GetMaxTraceableBlocks();
            protocol["maxvaliduntilblockincrement"] = system.GetMaxValidUntilBlockIncrement();
            protocol["maxtransactionsperblock"] = system.Settings.MaxTransactionsPerBlock;
            protocol["memorypoolmaxtransactions"] = system.Settings.MemoryPoolMaxTransactions;
            protocol["initialgasdistribution"] = system.Settings.InitialGasDistribution;
            protocol["hardforks"] = new JArray(system.Settings.Hardforks.Select(hf =>
            {
                JObject forkJson = new();
                // Strip "HF_" prefix.
                forkJson["name"] = StripPrefix(hf.Key.ToString(), "HF_");
                forkJson["blockheight"] = hf.Value;
                return forkJson;
            }));
            protocol["standbycommittee"] = new JArray(system.Settings.StandbyCommittee.Select(u => new JString(u.ToString())));
            protocol["seedlist"] = new JArray(system.Settings.SeedList.Select(u => new JString(u)));
            json["rpc"] = rpc;
            json["protocol"] = protocol;
            return json;
        }

        /// <summary>
        /// Removes a specified prefix from a string if it exists.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <param name="prefix">The prefix to remove.</param>
        /// <returns>The string with the prefix removed if it existed, otherwise the original string.</returns>
        private static string StripPrefix(string s, string prefix)
        {
            return s.StartsWith(prefix) ? s.Substring(prefix.Length) : s;
        }
    }
}

