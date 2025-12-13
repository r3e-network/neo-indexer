// Copyright (C) 2015-2025 The Neo Project.
//
// UT_RpcServer.Traces.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Neo.Plugins.RpcServer.Tests
{
    public partial class UT_RpcServer
    {
        private static MethodInfo GetPrivateStaticRpcServerMethod(string name)
        {
            return typeof(RpcServer).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new AssertFailedException($"Expected RpcServer private static method '{name}'");
        }

        private static Exception InvokePrivateStaticExpectingException(string name, params object[] args)
        {
            var method = GetPrivateStaticRpcServerMethod(name);
            try
            {
                method.Invoke(null, args);
                throw new AssertFailedException($"Expected exception from '{name}'");
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return ex.InnerException;
            }
        }

        [TestMethod]
        public void BuildSupabaseUri_FormatsAndEscapesQueryParameters()
        {
            var method = GetPrivateStaticRpcServerMethod("BuildSupabaseUri");
            var parameters = new List<KeyValuePair<string, string>>
            {
                new("select", "*"),
                new("block_index", "eq.1"),
                new("limit", "10"),
                new("empty", null)
            };

            var uri = (string)method.Invoke(null, new object[]
            {
                "https://example.supabase.co/",
                "opcode_traces",
                parameters
            });

            Assert.AreEqual(
                "https://example.supabase.co/rest/v1/opcode_traces?select=%2A&block_index=eq.1&limit=10",
                uri);
        }

        [TestMethod]
        public void EnsureSupabaseTraceSettings_ThrowsWhenRecorderDisabled()
        {
            var previousEnabled = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED");
            var previousUrl = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL");
            var previousKey = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY");

            try
            {
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED", "false");
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL", "https://example.supabase.co");
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY", "test");

                var ex = InvokePrivateStaticExpectingException("EnsureSupabaseTraceSettings");
                Assert.IsInstanceOfType(ex, typeof(RpcException));
                StringAssert.Contains(ex.Message, "state recorder not enabled");
            }
            finally
            {
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED", previousEnabled);
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL", previousUrl);
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY", previousKey);
            }
        }

        [TestMethod]
        public void EnsureSupabaseTraceSettings_ThrowsWhenSupabaseNotConfigured()
        {
            var previousEnabled = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED");
            var previousUrl = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL");
            var previousKey = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY");

            try
            {
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED", "true");
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL", null);
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY", null);

                var ex = InvokePrivateStaticExpectingException("EnsureSupabaseTraceSettings");
                Assert.IsInstanceOfType(ex, typeof(RpcException));
                StringAssert.Contains(ex.Message, "supabase connection not configured");
            }
            finally
            {
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED", previousEnabled);
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL", previousUrl);
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY", previousKey);
            }
        }

        [TestMethod]
        public void EnsureSupabaseTraceSettings_UsesRpcOverrideKeyWhenConfigured()
        {
            var previousEnabled = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED");
            var previousUrl = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL");
            var previousKey = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY");
            var previousOverrideKey = Environment.GetEnvironmentVariable("NEO_RPC_TRACES__SUPABASE_KEY");

            try
            {
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED", "true");
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL", "https://example.supabase.co");
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY", "service-role");
                Environment.SetEnvironmentVariable("NEO_RPC_TRACES__SUPABASE_KEY", "anon");

                var method = GetPrivateStaticRpcServerMethod("EnsureSupabaseTraceSettings");
                var result = (StateRecorderSettings)method.Invoke(null, Array.Empty<object>());

                Assert.AreEqual("https://example.supabase.co", result.SupabaseUrl);
                Assert.AreEqual("anon", result.SupabaseApiKey);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__ENABLED", previousEnabled);
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_URL", previousUrl);
                Environment.SetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_KEY", previousKey);
                Environment.SetEnvironmentVariable("NEO_RPC_TRACES__SUPABASE_KEY", previousOverrideKey);
            }
        }
    }
}
