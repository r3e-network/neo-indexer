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

            try
            {
                object? returnValue = descriptor.Handler.Invoke(this, parameters);
                if (descriptor.Handler.ReturnType != typeof(void))
                    Push(Convert(returnValue));
            }
            finally
            {
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
                            actualGasCost);
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
    }
}
