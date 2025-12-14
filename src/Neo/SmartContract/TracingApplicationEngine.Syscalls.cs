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
            ValidateCallFlags(descriptor.RequiredCallFlags);
            long gasCost = descriptor.FixedPrice * ExecFeeFactor;
            AddFee(gasCost);

            object[] parameters = new object[descriptor.Parameters.Count];
            for (int i = 0; i < parameters.Length; i++)
                parameters[i] = Convert(Pop(), descriptor.Parameters[i]);

            StorageTraceScope? storageScope = PrepareStorageScope(descriptor, parameters);
            int notificationBaseline = ShouldTrace(ExecutionTraceLevel.Notifications) ? Notifications.Count : 0;

            if (ShouldTrace(ExecutionTraceLevel.Syscalls))
            {
                _traceRecorder.RecordSyscall(
                    CurrentScriptHash,
                    descriptor.Hash,
                    descriptor.Name,
                    gasCost);
            }

            object? returnValue = descriptor.Handler.Invoke(this, parameters);
            if (descriptor.Handler.ReturnType != typeof(void))
                Push(Convert(returnValue));

            if (storageScope is not null)
                RecordStorageScope(storageScope);

            if (ShouldTrace(ExecutionTraceLevel.Notifications))
                RecordNotifications(notificationBaseline);
        }
    }
}
