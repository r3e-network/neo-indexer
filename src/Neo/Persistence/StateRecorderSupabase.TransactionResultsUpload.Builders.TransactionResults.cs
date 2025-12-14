// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.TransactionResultsUpload.Builders.TransactionResults.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Text.Json;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private static TransactionResultRow BuildTransactionResultRow(
            int blockIndex,
            string txHash,
            ExecutionTraceRecorder recorder)
        {
            var stats = recorder.GetStats();
            var vmState = recorder.VmState;

            return new TransactionResultRow(
                blockIndex,
                txHash,
                (int)vmState,
                vmState.ToString(),
                vmState == Neo.VM.VMState.HALT,
                stats.TotalGasConsumed,
                recorder.FaultException,
                ParseResultStackJson(recorder.ResultStackJson),
                stats.OpCodeCount,
                stats.SyscallCount,
                stats.ContractCallCount,
                stats.StorageWriteCount,
                stats.NotificationCount,
                recorder.LogCount);
        }

        private static JsonElement? ParseResultStackJson(string? stackJson)
        {
            if (string.IsNullOrWhiteSpace(stackJson))
                return null;

            try
            {
                using var document = JsonDocument.Parse(stackJson);
                return document.RootElement.Clone();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
