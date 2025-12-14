// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.Core.Rows.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Generic;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static (
            List<OpCodeTraceRow> OpCodeRows,
            List<SyscallTraceRow> SyscallRows,
            List<ContractCallTraceRow> ContractCallRows,
            List<StorageWriteTraceRow> StorageWriteRows,
            List<NotificationTraceRow> NotificationRows,
            List<RuntimeLogTraceRow> RuntimeLogRows) BuildTraceRows(
            int blockIndex,
            string txHash,
            ExecutionTraceRecorder recorder)
        {
            var contractHashCache = new Dictionary<UInt160, string>();

            var opCodeRows = BuildOpCodeTraceRows(blockIndex, txHash, recorder.GetOpCodeTraces(), contractHashCache);
            var syscallRows = BuildSyscallTraceRows(blockIndex, txHash, recorder.GetSyscallTraces(), contractHashCache);
            var contractCallRows = BuildContractCallTraceRows(blockIndex, txHash, recorder.GetContractCallTraces(), contractHashCache);
            var storageWriteRows = BuildStorageWriteTraceRows(blockIndex, txHash, recorder.GetStorageWriteTraces(), contractHashCache);
            var notificationRows = BuildNotificationTraceRows(blockIndex, txHash, recorder.GetNotificationTraces(), contractHashCache);
            var runtimeLogRows = BuildRuntimeLogTraceRows(blockIndex, txHash, recorder.GetLogTraces(), contractHashCache);

            return (opCodeRows, syscallRows, contractCallRows, storageWriteRows, notificationRows, runtimeLogRows);
        }
    }
}
