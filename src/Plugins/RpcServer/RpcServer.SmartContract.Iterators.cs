// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.SmartContract.Iterators.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.SmartContract.Iterators;
using System;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        [RpcMethod]
        protected internal virtual JToken TraverseIterator(JArray _params)
        {
            settings.SessionEnabled.True_Or(RpcError.SessionsDisabled);
            Guid sid = Result.Ok_Or(() => Guid.Parse(_params[0].GetString()), RpcError.InvalidParams.WithData($"Invalid session id {nameof(sid)}"));
            Guid iid = Result.Ok_Or(() => Guid.Parse(_params[1].GetString()), RpcError.InvalidParams.WithData($"Invliad iterator id {nameof(iid)}"));
            int count = _params[2].GetInt32();
            Result.True_Or(() => count <= settings.MaxIteratorResultItems, RpcError.InvalidParams.WithData($"Invalid iterator items count {nameof(count)}"));
            Session session;
            lock (sessions)
            {
                session = Result.Ok_Or(() => sessions[sid], RpcError.UnknownSession);
                session.ResetExpiration();
            }
            IIterator iterator = Result.Ok_Or(() => session.Iterators[iid], RpcError.UnknownIterator);
            JArray json = new();
            while (count-- > 0 && iterator.Next())
                json.Add(iterator.Value(null).ToJson());
            return json;
        }

        [RpcMethod]
        protected internal virtual JToken TerminateSession(JArray _params)
        {
            settings.SessionEnabled.True_Or(RpcError.SessionsDisabled);
            Guid sid = Result.Ok_Or(() => Guid.Parse(_params[0].GetString()), RpcError.InvalidParams.WithData("Invalid session id"));

            Session session = null;
            bool result;
            lock (sessions)
            {
                result = Result.Ok_Or(() => sessions.Remove(sid, out session), RpcError.UnknownSession);
            }
            if (result) session.Dispose();
            return result;
        }
    }
}
