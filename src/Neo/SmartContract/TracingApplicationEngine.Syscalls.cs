// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngine.Syscalls.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Extensions;
using Neo.Persistence;
using System;

namespace Neo.SmartContract
{
    public partial class TracingApplicationEngine
    {
        /// <summary>
        /// Handles syscall execution while emitting tracing information.
        /// </summary>
        protected override void OnSysCall(InteropDescriptor descriptor)
        {
            var feeBefore = FeeConsumed;
            ValidateCallFlags(descriptor.RequiredCallFlags);
            long baseGasCost = descriptor.FixedPrice * ExecFeeFactor;
            AddFee(baseGasCost);

            object[] parameters = new object[descriptor.Parameters.Count];
            for (int i = 0; i < parameters.Length; i++)
                parameters[i] = Convert(Pop(), descriptor.Parameters[i]);

            StorageTraceScope? storageScope = PrepareStorageScope(descriptor, parameters);
            int notificationBaseline = ShouldTrace(ExecutionTraceLevel.Notifications) ? Notifications.Count : 0;
            var shouldTraceLogs = ShouldTrace(ExecutionTraceLevel.Logs);
            var callSucceeded = false;
            IDisposable? readSourceScope = null;

            try
            {
                if (StateReadRecorder.IsRecording)
                    readSourceScope = StateReadRecorder.BeginSource(descriptor.Name);

                object? returnValue = descriptor.Handler.Invoke(this, parameters);
                callSucceeded = true;
                if (descriptor.Handler.ReturnType != typeof(void))
                    Push(Convert(returnValue));
            }
            finally
            {
                readSourceScope?.Dispose();

                if (callSucceeded && shouldTraceLogs && string.Equals(descriptor.Name, "System.Runtime.Log", System.StringComparison.Ordinal))
                {
                    try
                    {
                        var message = ExtractRuntimeLogMessage(parameters);
                        if (message is not null)
                            _traceRecorder.RecordLog(CurrentScriptHash, message);
                    }
                    catch
                    {
                        // Best-effort tracing; never fail syscall execution.
                    }
                }

                if (ShouldTrace(ExecutionTraceLevel.Syscalls))
                {
                    try
                    {
                        var actualGasCost = FeeConsumed - feeBefore;
                        if (actualGasCost < 0)
                            actualGasCost = 0;

                        _traceRecorder.RecordSyscall(
                            CurrentScriptHash,
                            descriptor.Hash,
                            descriptor.Name,
                            actualGasCost,
                            success: callSucceeded);
                    }
                    catch
                    {
                        // Best-effort tracing; never fail syscall execution.
                    }
                }

                if (storageScope is not null)
                {
                    try
                    {
                        RecordStorageScope(storageScope);
                    }
                    catch
                    {
                        // Best-effort tracing; never fail syscall execution.
                    }
                }

                if (ShouldTrace(ExecutionTraceLevel.Notifications))
                {
                    try
                    {
                        RecordNotifications(notificationBaseline);
                    }
                    catch
                    {
                        // Best-effort tracing; never fail syscall execution.
                    }
                }
            }
        }

        private static string? ExtractRuntimeLogMessage(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is null)
                return null;

            return parameters[0] switch
            {
                string s => s,
                byte[] bytes => bytes.ToStrictUtf8String(),
                System.ReadOnlyMemory<byte> memory => memory.Span.ToArray().ToStrictUtf8String(),
                _ => parameters[0].ToString()
            };
        }
    }
}
