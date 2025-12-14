// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.RequestHandling.cs file belongs to the neo project and is free
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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

        public async Task ProcessAsync(HttpContext context)
        {
            if (context.Request.Method != "GET" && context.Request.Method != "POST") return;
            JToken request = null;
            if (context.Request.Method == "GET")
            {
                string jsonrpc = context.Request.Query["jsonrpc"];
                string id = context.Request.Query["id"];
                string method = context.Request.Query["method"];
                string _params = context.Request.Query["params"];
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(method) && !string.IsNullOrEmpty(_params))
                {
                    try
                    {
                        _params = Encoding.UTF8.GetString(Convert.FromBase64String(_params));
                    }
                    catch (FormatException) { }
                    request = new JObject();
                    if (!string.IsNullOrEmpty(jsonrpc))
                        request["jsonrpc"] = jsonrpc;
                    request["id"] = id;
                    request["method"] = method;
                    request["params"] = JToken.Parse(_params, MaxParamsDepth);
                }
            }
            else if (context.Request.Method == "POST")
            {
                using StreamReader reader = new(context.Request.Body);
                try
                {
                    request = JToken.Parse(await reader.ReadToEndAsync(), MaxParamsDepth);
                }
                catch (FormatException) { }
            }
            JToken response;
            if (request == null)
            {
                response = CreateErrorResponse(null, RpcError.BadRequest);
            }
            else if (request is JArray array)
            {
                if (array.Count == 0)
                {
                    response = CreateErrorResponse(request["id"], RpcError.InvalidRequest);
                }
                else
                {
                    var tasks = array.Select(p => ProcessRequestAsync(context, (JObject)p));
                    var results = await Task.WhenAll(tasks);
                    response = results.Where(p => p != null).ToArray();
                }
            }
            else
            {
                response = await ProcessRequestAsync(context, (JObject)request);
            }
            if (response == null || (response as JArray)?.Count == 0) return;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(response.ToString(), Encoding.UTF8);
        }

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

