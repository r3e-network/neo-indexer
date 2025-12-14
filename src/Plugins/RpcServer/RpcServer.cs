// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Microsoft.AspNetCore.Hosting;
using Neo.Json;
using Neo.Network.P2P;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.Plugins.RpcServer
{
    public partial class RpcServer : IDisposable
    {
        private const int MaxParamsDepth = 32;

        private readonly Dictionary<string, Func<JArray, object>> methods = new();
        private readonly Dictionary<string, Delegate> _methodsWithParams = new();

        private IWebHost host;
        private RpcServerSettings settings;
        private readonly NeoSystem system;
        private readonly LocalNode localNode;

        // avoid GetBytes every time
        private readonly byte[] _rpcUser;
        private readonly byte[] _rpcPass;

        public RpcServer(NeoSystem system, RpcServerSettings settings)
        {
            this.system = system;
            this.settings = settings;

            _rpcUser = settings.RpcUser is not null ? Encoding.UTF8.GetBytes(settings.RpcUser) : [];
            _rpcPass = settings.RpcPass is not null ? Encoding.UTF8.GetBytes(settings.RpcPass) : [];

            localNode = system.LocalNode.Ask<LocalNode>(new LocalNode.GetInstance()).Result;
            RegisterMethods(this);
            Initialize_SmartContract();
        }

        public void Dispose()
        {
            Dispose_SmartContract();
            if (host != null)
            {
                host.Dispose();
                host = null;
            }
        }

        internal void UpdateSettings(RpcServerSettings settings)
        {
            this.settings = settings;
        }
    }
}
