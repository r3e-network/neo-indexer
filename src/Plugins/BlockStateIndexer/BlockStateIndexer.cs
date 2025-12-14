// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.IEventHandlers;
using Neo.Ledger;
using Neo.SmartContract;
using Neo.Plugins;
using static System.IO.Path;

namespace Neo.Plugins.BlockStateIndexer
{
    /// <summary>
    /// Plugin that captures storage reads and execution traces during block persistence
    /// and uploads derived data to Supabase for analytics and replay tooling.
    /// </summary>
    public sealed partial class BlockStateIndexerPlugin : Plugin, ICommittingHandler, ICommittedHandler
    {
        private global::Neo.NeoSystem? _neoSystem;
        private TracingApplicationEngineProvider? _tracingProvider;
        private IApplicationEngineProvider? _previousEngineProvider;

        public override string Name => "BlockStateIndexer";
        public override string Description => "Indexes block execution (reads/results/traces) into Supabase.";
        protected override UnhandledExceptionPolicy ExceptionPolicy => Settings.Default.ExceptionPolicy;
        public override string ConfigFile => Combine(RootPath, "BlockStateIndexer.json");

        public BlockStateIndexerPlugin()
        {
            Blockchain.Committing += ((ICommittingHandler)this).Blockchain_Committing_Handler;
            Blockchain.Committed += ((ICommittedHandler)this).Blockchain_Committed_Handler;
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }
    }
}
