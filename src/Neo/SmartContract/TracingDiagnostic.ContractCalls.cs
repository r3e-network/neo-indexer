// Copyright (C) 2015-2025 The Neo Project.
//
// TracingDiagnostic.ContractCalls.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.VM;
using System.Collections.Generic;

namespace Neo.SmartContract
{
    public sealed partial class TracingDiagnostic
    {
        /// <summary>
        /// Called when a new execution context is loaded (contract call starts).
        /// </summary>
        public void ContextLoaded(ExecutionContext context)
        {
            if (!TraceContractCalls || _engine == null) return;

            var calleeHash = context.GetScriptHash();
            var callerHash = _engine.CallingScriptHash;
            var callDepth = _engine.InvocationStack.Count;

            string? methodName = null;
            try
            {
                var state = context.GetState<ExecutionContextState>();
                var contract = state.Contract;
                var methods = contract?.Manifest?.Abi?.Methods;
                var updateCounter = contract?.UpdateCounter ?? (ushort)0;
                var offset = context.InstructionPointer;

                if (methods is { Length: > 0 })
                {
                    if (!_methodNameCache.TryGetValue(calleeHash, out var cached) || cached.UpdateCounter != updateCounter)
                    {
                        var offsetMap = new Dictionary<int, string?>(methods.Length);
                        foreach (var method in methods)
                            offsetMap[method.Offset] = method.Name;
                        cached = (updateCounter, offsetMap);
                        _methodNameCache[calleeHash] = cached;
                    }

                    cached.Offsets.TryGetValue(offset, out methodName);
                }
            }
            catch
            {
                methodName = null;
            }

            // Record the contract call
            var trace = _recorder.RecordContractCall(
                callerHash,
                calleeHash,
                methodName: methodName,
                callDepth);

            // Push to call stack for tracking completion
            _callStack.Push((trace, _engine.FeeConsumed));
        }

        /// <summary>
        /// Called when an execution context is unloaded (contract call ends).
        /// </summary>
        public void ContextUnloaded(ExecutionContext context)
        {
            if (!TraceContractCalls || _engine == null) return;

            if (_callStack.Count > 0)
            {
                var (trace, gasStart) = _callStack.Pop();
                var gasConsumed = _engine.FeeConsumed - gasStart;

                trace.GasConsumed = gasConsumed < 0 ? 0 : gasConsumed;
                if (_engine.State == VMState.FAULT || _engine.FaultException is not null)
                    trace.Success = false;
            }
        }
    }
}
