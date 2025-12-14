// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.RequestHandling.Responses.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System;
using System.Reflection;

namespace Neo.Plugins.RpcServer
{
    public partial class RpcServer
    {
        private static JObject CreateErrorResponse(JToken id, RpcError rpcError)
        {
            JObject response = CreateResponse(id);
            response["error"] = rpcError.ToJson();
            return response;
        }

        private static JObject CreateResponse(JToken id)
        {
            JObject response = new();
            response["jsonrpc"] = "2.0";
            response["id"] = id;
            return response;
        }

        /// <summary>
        /// Unwraps an exception to get the original exception.
        /// This is particularly useful for TargetInvocationException and AggregateException which wrap the actual exception.
        /// </summary>
        /// <param name="ex">The exception to unwrap</param>
        /// <returns>The unwrapped exception</returns>
        private static Exception UnwrapException(Exception ex)
        {
            if (ex is TargetInvocationException targetEx && targetEx.InnerException != null)
                return targetEx.InnerException;

            // Also handle AggregateException with a single inner exception
            if (ex is AggregateException aggEx && aggEx.InnerExceptions.Count == 1)
                return aggEx.InnerExceptions[0];

            return ex;
        }
    }
}

