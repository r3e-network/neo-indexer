// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TraceUpload.Core.Postgres.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static Task TryUploadBlockTracePostgresAsync(
            int blockIndex,
            string txHash,
            List<OpCodeTraceRow> opCodeRows,
            List<SyscallTraceRow> syscallRows,
            List<ContractCallTraceRow> contractCallRows,
            List<StorageWriteTraceRow> storageWriteRows,
            List<NotificationTraceRow> notificationRows,
            List<RuntimeLogTraceRow> runtimeLogRows,
            int batchSize,
            bool trimStaleTraceRows,
            StateRecorderSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.SupabaseConnectionString))
                return Task.CompletedTask;

#if NET9_0_OR_GREATER
            return UploadBlockTracePostgresAsync(
                blockIndex,
                txHash,
                opCodeRows,
                syscallRows,
                contractCallRows,
                storageWriteRows,
                notificationRows,
                runtimeLogRows,
                batchSize,
                trimStaleTraceRows,
                settings);
#else
            return Task.CompletedTask;
#endif
        }
    }
}
