// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.RequestHandling.Auth.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Http;
using System;
using System.Security.Cryptography;

namespace Neo.Plugins.RpcServer
{
    public partial class RpcServer
    {
        internal bool CheckAuth(HttpContext context)
        {
            if (string.IsNullOrEmpty(settings.RpcUser)) return true;

            string reqauth = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(reqauth))
            {
                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Restricted\"";
                context.Response.StatusCode = 401;
                return false;
            }

            byte[] auths;
            try
            {
                auths = Convert.FromBase64String(reqauth.Replace("Basic ", "").Trim());
            }
            catch
            {
                return false;
            }

            int colonIndex = Array.IndexOf(auths, (byte)':');
            if (colonIndex == -1)
                return false;

            byte[] user = auths[..colonIndex];
            byte[] pass = auths[(colonIndex + 1)..];

            // Always execute both checks, but both must evaluate to true
            return CryptographicOperations.FixedTimeEquals(user, _rpcUser) & CryptographicOperations.FixedTimeEquals(pass, _rpcPass);
        }
    }
}

