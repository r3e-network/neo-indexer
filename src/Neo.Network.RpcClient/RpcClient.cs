// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Json;
using Neo.Network.RPC.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    /// <summary>
    /// The RPC client to call NEO RPC methods
    /// </summary>
    public partial class RpcClient : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly Uri baseAddress;
        internal readonly ProtocolSettings protocolSettings;

        public RpcClient(Uri url, string rpcUser = default, string rpcPass = default, ProtocolSettings protocolSettings = null)
        {
            httpClient = new HttpClient();
            baseAddress = url;
            if (!string.IsNullOrEmpty(rpcUser) && !string.IsNullOrEmpty(rpcPass))
            {
                string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{rpcUser}:{rpcPass}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            }
            this.protocolSettings = protocolSettings ?? ProtocolSettings.Default;
        }

        public RpcClient(HttpClient client, Uri url, ProtocolSettings protocolSettings = null)
        {
            httpClient = client;
            baseAddress = url;
            this.protocolSettings = protocolSettings ?? ProtocolSettings.Default;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    httpClient.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

        static RpcRequest AsRpcRequest(string method, params JToken[] paraArgs)
        {
            return new RpcRequest
            {
                Id = 1,
                JsonRpc = "2.0",
                Method = method,
                Params = paraArgs
            };
        }

        static RpcResponse AsRpcResponse(string content, bool throwOnError)
        {
            var response = RpcResponse.FromJson((JObject)JToken.Parse(content));
            response.RawResponse = content;

            if (response.Error != null && throwOnError)
            {
                throw new RpcException(response.Error.Code, response.Error.Message);
            }

            return response;
        }

        HttpRequestMessage AsHttpRequest(RpcRequest request)
        {
            var requestJson = request.ToJson().ToString();
            return new HttpRequestMessage(HttpMethod.Post, baseAddress)
            {
                Content = new StringContent(requestJson, Neo.Utility.StrictUTF8)
            };
        }

        public RpcResponse Send(RpcRequest request, bool throwOnError = true)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(RpcClient));

            using var requestMsg = AsHttpRequest(request);
            using var responseMsg = httpClient.Send(requestMsg);
            using var contentStream = responseMsg.Content.ReadAsStream();
            using var contentReader = new StreamReader(contentStream);
            return AsRpcResponse(contentReader.ReadToEnd(), throwOnError);
        }

        public async Task<RpcResponse> SendAsync(RpcRequest request, bool throwOnError = true)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(RpcClient));

            using var requestMsg = AsHttpRequest(request);
            using var responseMsg = await httpClient.SendAsync(requestMsg).ConfigureAwait(false);
            var content = await responseMsg.Content.ReadAsStringAsync();
            return AsRpcResponse(content, throwOnError);
        }

        public virtual JToken RpcSend(string method, params JToken[] paraArgs)
        {
            var request = AsRpcRequest(method, paraArgs);
            var response = Send(request);
            return response.Result;
        }

        public virtual async Task<JToken> RpcSendAsync(string method, params JToken[] paraArgs)
        {
            var request = AsRpcRequest(method, paraArgs);
            var response = await SendAsync(request).ConfigureAwait(false);
            return response.Result;
        }

        public static string GetRpcName([CallerMemberName] string methodName = null)
        {
            return new Regex("(.*?)(Hex|Both)?(Async)?").Replace(methodName, "$1").ToLowerInvariant();
        }
    }
}
