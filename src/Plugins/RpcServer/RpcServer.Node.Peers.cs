// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Node.Peers.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System.Linq;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Gets the current number of connections to the node.
        /// </summary>
        /// <returns>The number of connections as a JToken.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetConnectionCount()
        {
            return localNode.ConnectedCount;
        }

        /// <summary>
        /// Gets information about the peers connected to the node.
        /// </summary>
        /// <returns>A JObject containing information about unconnected, bad, and connected peers.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetPeers()
        {
            JObject json = new();
            json["unconnected"] = new JArray(localNode.GetUnconnectedPeers().Select(p =>
            {
                JObject peerJson = new();
                peerJson["address"] = p.Address.ToString();
                peerJson["port"] = p.Port;
                return peerJson;
            }));
            json["bad"] = new JArray(); //badpeers has been removed
            json["connected"] = new JArray(localNode.GetRemoteNodes().Select(p =>
            {
                JObject peerJson = new();
                peerJson["address"] = p.Remote.Address.ToString();
                peerJson["port"] = p.ListenerTcpPort;
                return peerJson;
            }));
            return json;
        }
    }
}

