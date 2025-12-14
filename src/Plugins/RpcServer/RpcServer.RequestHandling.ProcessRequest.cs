// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.RequestHandling.ProcessRequest.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Http;
using Neo.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Plugins.RpcServer
{
    public partial class RpcServer
    {
        internal async Task<JObject> ProcessRequestAsync(HttpContext context, JObject request)
        {
            if (!request.ContainsProperty("id")) return null;
            var @params = request["params"] ?? new JArray();
            if (!request.ContainsProperty("method") || @params is not JArray)
            {
                return CreateErrorResponse(request["id"], RpcError.InvalidRequest);
            }

            var jsonParameters = (JArray)@params;
            var response = CreateResponse(request["id"]);
            try
            {
                var method = request["method"].AsString();
                (CheckAuth(context) && !settings.DisabledMethods.Contains(method)).True_Or(RpcError.AccessDenied);

                if (methods.TryGetValue(method, out var func))
                {
                    response["result"] = func(jsonParameters) switch
                    {
                        JToken result => result,
                        Task<JToken> task => await task,
                        _ => throw new NotSupportedException()
                    };
                    return response;
                }

                if (_methodsWithParams.TryGetValue(method, out var func2))
                {
                    var paramInfos = func2.Method.GetParameters();
                    var args = new object[paramInfos.Length];

                    for (var i = 0; i < paramInfos.Length; i++)
                    {
                        var param = paramInfos[i];
                        if (jsonParameters.Count > i && jsonParameters[i] != null)
                        {
                            try
                            {
                                if (param.ParameterType == typeof(UInt160))
                                {
                                    args[i] = ParameterConverter.ConvertUInt160(jsonParameters[i],
                                        system.Settings.AddressVersion);
                                }
                                else
                                {
                                    args[i] = ParameterConverter.ConvertParameter(jsonParameters[i],
                                        param.ParameterType);
                                }
                            }
                            catch (Exception e) when (e is not RpcException)
                            {
                                throw new ArgumentException($"Invalid value for parameter '{param.Name}'", e);
                            }
                        }
                        else
                        {
                            if (param.IsOptional)
                            {
                                args[i] = param.DefaultValue;
                            }
                            else if (param.ParameterType.IsValueType &&
                                     Nullable.GetUnderlyingType(param.ParameterType) == null)
                            {
                                throw new ArgumentException($"Required parameter '{param.Name}' is missing");
                            }
                            else
                            {
                                args[i] = null;
                            }
                        }
                    }

                    response["result"] = func2.DynamicInvoke(args) switch
                    {
                        JToken result => result,
                        Task<JToken> task => await task,
                        _ => throw new NotSupportedException()
                    };
                    return response;
                }

                throw new RpcException(RpcError.MethodNotFound.WithData(method));
            }
            catch (FormatException ex)
            {
                return CreateErrorResponse(request["id"], RpcError.InvalidParams.WithData(ex.Message));
            }
            catch (IndexOutOfRangeException ex)
            {
                return CreateErrorResponse(request["id"], RpcError.InvalidParams.WithData(ex.Message));
            }
            catch (Exception ex) when (ex is not RpcException)
            {
                // Unwrap the exception to get the original error code
                var unwrappedException = UnwrapException(ex);
#if DEBUG
                return CreateErrorResponse(request["id"],
                    RpcErrorFactory.NewCustomError(unwrappedException.HResult, unwrappedException.Message, unwrappedException.StackTrace));
#else
                return CreateErrorResponse(request["id"], RpcErrorFactory.NewCustomError(unwrappedException.HResult, unwrappedException.Message));
#endif
            }
            catch (RpcException ex)
            {
#if DEBUG
                return CreateErrorResponse(request["id"],
                    RpcErrorFactory.NewCustomError(ex.HResult, ex.Message, ex.StackTrace));
#else
                return CreateErrorResponse(request["id"], ex.GetError());
#endif
            }
        }
    }
}

