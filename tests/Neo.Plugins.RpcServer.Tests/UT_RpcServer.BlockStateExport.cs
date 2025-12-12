// Copyright (C) 2015-2025 The Neo Project.
//
// UT_RpcServer.BlockStateExport.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Json;
using Neo.Ledger;
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract.Native;
using System;

namespace Neo.Plugins.RpcServer.Tests
{
    public partial class UT_RpcServer
    {
        [TestMethod]
        public void TestGetBlockStateExport_PathAndUrls()
        {
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED", "true");
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL", "https://example.supabase.co");
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY", "test");
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__UPLOAD_MODE", "Both");
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__UPLOAD_AUX_FORMATS", "true");
            var snapshot = _neoSystem.GetSnapshotCache();
            var block = NativeContract.Ledger.GetBlock(snapshot, 0);
            Assert.IsNotNull(block);

            var result = _rpcServer.GetBlockStateExport(0);
            Assert.AreEqual("block-0.bin", result["path"].AsString());
            StringAssert.Contains(result["publicUrl"].AsString(), "block-0.bin");
            StringAssert.Contains(result["authUrl"].AsString(), "block-0.bin");
            Assert.AreEqual("bin", result["format"].AsString());

            var csv = _rpcServer.GetBlockStateExport(0, "csv");
            Assert.AreEqual("block-0.csv", csv["path"].AsString());
            StringAssert.Contains(csv["publicUrl"].AsString(), "block-0.csv");
            Assert.AreEqual("csv", csv["format"].AsString());

            var json = _rpcServer.GetBlockStateExport(0, "json");
            Assert.AreEqual("block-0.json", json["path"].AsString());
            StringAssert.Contains(json["publicUrl"].AsString(), "block-0.json");
            Assert.AreEqual("json", json["format"].AsString());

            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED", null);
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL", null);
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY", null);
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__UPLOAD_MODE", null);
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__UPLOAD_AUX_FORMATS", null);
        }

        [TestMethod]
        public void TestGetBlockStateExport_AuxFormatsDisabled()
        {
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED", "true");
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL", "https://example.supabase.co");
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY", "test");
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__UPLOAD_MODE", "Both");
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__UPLOAD_AUX_FORMATS", "false");

            Assert.ThrowsExactly<RpcException>(() => _rpcServer.GetBlockStateExport(0, "json"));
            Assert.ThrowsExactly<RpcException>(() => _rpcServer.GetBlockStateExport(0, "csv"));

            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED", null);
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL", null);
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY", null);
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__UPLOAD_MODE", null);
            Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__UPLOAD_AUX_FORMATS", null);
        }
    }
}
