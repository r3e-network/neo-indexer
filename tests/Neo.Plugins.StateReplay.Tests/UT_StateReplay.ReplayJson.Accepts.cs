// Copyright (C) 2015-2025 The Neo Project.
//
// UT_StateReplay.ReplayJson.Accepts.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.IO;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace StateReplay.Tests
{
    public partial class UT_StateReplay
    {
        [TestMethod]
        public void ReplayAcceptsMatchingSnapshot()
        {
            var path = Path.GetTempFileName();
            var key = StorageKey.Create(0, 0x01);
            var doc = new
            {
                block = 0u,
                hash = NativeContract.Ledger.GetBlockHash(_system.StoreView, 0)?.ToString(),
                keyCount = 1,
                keys = new[]
                {
                    new {
                        key = Convert.ToBase64String(key.ToArray()),
                        value = Convert.ToBase64String(new byte[]{0x01}),
                        readOrder = 1
                    }
                }
            };
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(doc));
            _plugin.ReplayForTest(path, 0);
        }

        [TestMethod]
        public void ReplayAcceptsEmptyValue()
        {
            var path = Path.GetTempFileName();
            var key = StorageKey.Create(0, 0x01);
            var doc = new
            {
                block = 0u,
                hash = NativeContract.Ledger.GetBlockHash(_system.StoreView, 0)?.ToString(),
                keyCount = 1,
                keys = new[]
                {
                    new {
                        key = Convert.ToBase64String(key.ToArray()),
                        value = "",
                        readOrder = 1
                    }
                }
            };
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(doc));
            _plugin.ReplayForTest(path, 0);
        }
    }
}

