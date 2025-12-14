// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StateRecorderSupabase_Traces.Models.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using Neo.VM;
using System.Text.Json;

namespace Neo.UnitTests.Persistence
{
    public sealed partial class UT_StateRecorderSupabase_Traces
    {
        [TestMethod]
        public void TraceRowModels_SerializeWithExpectedPropertyNames()
        {
            var txHash = SampleTxHash.ToString();
            var opcodeRow = new OpCodeTraceRow(100, txHash, 5, ExecutionContract.ToString(), 42, (int)OpCode.PUSH1, "PUSH1", "AQ==", 250, 2);
            var syscallRow = new SyscallTraceRow(100, txHash, 6, ExecutionContract.ToString(), "ABCD1234", "System.Runtime.Log", 77);
            var contractRow = new ContractCallTraceRow(100, txHash, 7, CallerContract.ToString(), CalleeContract.ToString(), "transfer", 2, false, 1500);

            using (var opcodeJson = JsonDocument.Parse(JsonSerializer.Serialize(opcodeRow)))
            {
                var element = opcodeJson.RootElement;
                Assert.AreEqual(100, element.GetProperty("block_index").GetInt32());
                Assert.AreEqual("PUSH1", element.GetProperty("opcode_name").GetString());
                Assert.AreEqual("AQ==", element.GetProperty("operand_base64").GetString());
                Assert.AreEqual(2, element.GetProperty("stack_depth").GetInt32());
            }

            using (var syscallJson = JsonDocument.Parse(JsonSerializer.Serialize(syscallRow)))
            {
                var element = syscallJson.RootElement;
                Assert.AreEqual("ABCD1234", element.GetProperty("syscall_hash").GetString());
                Assert.AreEqual("System.Runtime.Log", element.GetProperty("syscall_name").GetString());
                Assert.AreEqual(77, element.GetProperty("gas_cost").GetInt64());
            }

            using (var contractJson = JsonDocument.Parse(JsonSerializer.Serialize(contractRow)))
            {
                var element = contractJson.RootElement;
                Assert.AreEqual("transfer", element.GetProperty("method_name").GetString());
                Assert.IsFalse(element.GetProperty("success").GetBoolean());
                Assert.AreEqual(1500, element.GetProperty("gas_consumed").GetInt64());
                Assert.AreEqual(CallerContract.ToString(), element.GetProperty("caller_hash").GetString());
                Assert.AreEqual(CalleeContract.ToString(), element.GetProperty("callee_hash").GetString());
            }
        }
    }
}

