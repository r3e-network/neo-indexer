// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.Replay.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using System.Collections.Generic;

namespace StateReplay
{
    public partial class StateReplayPlugin
    {
        private void ReplayBlock(Block block, StoreCache snapshot)
        {
            var settings = _system!.Settings;
            var messages = new List<string>();

            using (ApplicationEngine engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, settings, 0))
            {
                engine.LoadScript(OnPersistScript);
                var state = engine.Execute();
                messages.Add($"OnPersist: {state}");
            }

            var clonedSnapshot = snapshot.CloneCache();
            foreach (Transaction tx in block.Transactions)
            {
                using ApplicationEngine engine = ApplicationEngine.Create(TriggerType.Application, tx, clonedSnapshot, block, settings, tx.SystemFee);
                engine.LoadScript(tx.Script);
                var state = engine.Execute();
                messages.Add($"Tx {tx.Hash}: {state}");
                if (state == VMState.HALT)
                    clonedSnapshot.Commit();
                else
                    clonedSnapshot = snapshot.CloneCache();
            }

            using (ApplicationEngine engine = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshot, block, settings, 0))
            {
                engine.LoadScript(PostPersistScript);
                var state = engine.Execute();
                messages.Add($"PostPersist: {state}");
            }

            ConsoleHelper.Info("Replay", $"Block {block.Index} ({block.Hash})");
            foreach (var line in messages)
            {
                ConsoleHelper.Info("Replay", line);
            }
        }
    }
}

