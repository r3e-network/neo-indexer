// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.MethodRegistration.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Neo.Plugins.RpcServer
{
    public partial class RpcServer
    {
        public void RegisterMethods(object handler)
        {
            foreach (var method in handler.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attribute = method.GetCustomAttribute<RpcMethodAttribute>();
                var attributeWithParams = method.GetCustomAttribute<RpcMethodWithParamsAttribute>();
                if (attribute is null && attributeWithParams is null) continue;
                if (attribute is not null && attributeWithParams is not null) throw new InvalidOperationException("Method cannot have both RpcMethodAttribute and RpcMethodWithParamsAttribute");

                if (attribute is not null)
                {
                    var name = string.IsNullOrEmpty(attribute.Name) ? method.Name.ToLowerInvariant() : attribute.Name;
                    methods[name] = method.CreateDelegate<Func<JArray, object>>(handler);
                }

                if (attributeWithParams is not null)
                {
                    var name = string.IsNullOrEmpty(attributeWithParams.Name) ? method.Name.ToLowerInvariant() : attributeWithParams.Name;

                    var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
                    var delegateType = Expression.GetDelegateType(parameters.Concat([method.ReturnType]).ToArray());

                    _methodsWithParams[name] = Delegate.CreateDelegate(delegateType, handler, method);
                }
            }
        }
    }
}

