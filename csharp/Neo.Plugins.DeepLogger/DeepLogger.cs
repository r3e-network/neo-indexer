using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace Neo.Plugins.DeepLogger;

// Persists VM traces to CSV for external ingestion.
public class DeepLogger : Plugin, IPersistencePlugin
{
    public override string Name => "DeepLogger";
    public override string Description => "Exports VM traces to CSV";

    private readonly string _logDirectory =
        Environment.GetEnvironmentVariable("DEEPLOGGER_LOG_DIR") ?? "/neo-data/logs";
    private readonly int _rotateBlocks =
        int.TryParse(Environment.GetEnvironmentVariable("DEEPLOGGER_ROTATE_BLOCKS"), out var r) && r > 0
            ? r
            : 1000;

    private StreamWriter? _traceWriter;
    private StreamWriter? _blockWriter;
    private StreamWriter? _txWriter;
    private int _currentFileIndex = -1;

    // Neo N3 Syscall hash to name mapping
    private static readonly Dictionary<uint, string> SyscallNames = new()
    {
        // System
        { 0x77777777, "System.Contract.Call" },
        { 0x627d5b52, "System.Contract.CallNative" },
        { 0x9bf667ce, "System.Contract.CreateStandardAccount" },
        { 0x83c8d4bf, "System.Contract.CreateMultisigAccount" },
        { 0xb3a9b7a1, "System.Contract.GetCallFlags" },
        { 0xf827ec8c, "System.Contract.NativeOnPersist" },
        { 0x7f82f0a6, "System.Contract.NativePostPersist" },
        { 0xcfed7c1d, "System.Crypto.CheckSig" },
        { 0x9ed0dc3a, "System.Crypto.CheckMultisig" },
        { 0xe63f1884, "System.Iterator.Next" },
        { 0x7c75dab8, "System.Iterator.Value" },
        { 0xdbfea874, "System.Runtime.Platform" },
        { 0xec41d938, "System.Runtime.GetTrigger" },
        { 0xc5c7a7c1, "System.Runtime.GetTime" },
        { 0x97003800, "System.Runtime.GetScriptContainer" },
        { 0x2d510830, "System.Runtime.GetExecutingScriptHash" },
        { 0xa55c2bfe, "System.Runtime.GetCallingScriptHash" },
        { 0xb7c38803, "System.Runtime.GetEntryScriptHash" },
        { 0xf6e5a2c5, "System.Runtime.CheckWitness" },
        { 0x274335f1, "System.Runtime.GetInvocationCounter" },
        { 0x7c3b9c9c, "System.Runtime.Log" },
        { 0x95016f61, "System.Runtime.Notify" },
        { 0x5c65d0a6, "System.Runtime.GetNotifications" },
        { 0x2a2e4e7a, "System.Runtime.GasLeft" },
        { 0x169f7c27, "System.Runtime.BurnGas" },
        { 0x6e59a141, "System.Runtime.GetNetwork" },
        { 0x4e815a23, "System.Runtime.GetRandom" },
        { 0x68774d21, "System.Runtime.GetAddressVersion" },
        { 0x7bc5a5e1, "System.Storage.GetContext" },
        { 0x2a2e4e7b, "System.Storage.GetReadOnlyContext" },
        { 0x8f4b5c1a, "System.Storage.AsReadOnly" },
        { 0x925de831, "System.Storage.Get" },
        { 0xf4a5c7e8, "System.Storage.Find" },
        { 0xe63f1885, "System.Storage.Put" },
        { 0x88e5b224, "System.Storage.Delete" },
    };

    protected override void Configure()
    {
        if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);
    }

    public void OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
    {
        // Rotate outputs to keep files small.
        int fileIndex = (int)(block.Index / _rotateBlocks);
        if (fileIndex != _currentFileIndex)
        {
            CloseWriters();
            _currentFileIndex = fileIndex;
            _blockWriter = new StreamWriter(Path.Combine(_logDirectory, $"blocks_{fileIndex}.csv"), true, Encoding.UTF8, 65536);
            _txWriter = new StreamWriter(Path.Combine(_logDirectory, $"txs_{fileIndex}.csv"), true, Encoding.UTF8, 65536);
            _traceWriter = new StreamWriter(Path.Combine(_logDirectory, $"trace_{fileIndex}.csv"), true, Encoding.UTF8, 65536);
        }

        // Block summary
        _blockWriter!.WriteLine($"{block.Index},{block.Hash},{block.Timestamp},{block.Transactions.Length}");

        foreach (var tx in block.Transactions)
        {
            // Skip transactions without executed trace.
            var appExec = applicationExecutedList.FirstOrDefault(p => p.Transaction?.Hash == tx.Hash);
            if (appExec == null) continue;

            var sender = tx.Signers.Length > 0 ? tx.Signers[0].Account.ToString() : string.Empty;
            _txWriter!.WriteLine($"{tx.Hash},{block.Index},{sender},{tx.SystemFee},{tx.NetworkFee}");

            using var store = system.LoadStore(readOnly: true);
            using var traceSnapshot = store.GetSnapshot();
            using var engine = ApplicationEngine.Create(
                TriggerType.Application,
                tx,
                traceSnapshot,
                persistingBlock: null,
                gas: tx.SystemFee
            );
            engine.LoadScript(tx.Script);

            int step = 0;
            while (engine.State != VMState.HALT && engine.State != VMState.FAULT && engine.State != VMState.BREAK)
            {
                var ctx = engine.CurrentContext;
                var op = ctx?.CurrentInstruction?.OpCode ?? OpCode.RET;
                var scriptHash = ctx?.ScriptHash?.ToString() ?? string.Empty;

                string sysCall = string.Empty;
                if (op == OpCode.SYSCALL && ctx?.CurrentInstruction?.Operand.Length >= 4)
                {
                    try
                    {
                        // Operand is the syscall hash (little-endian uint).
                        uint syscallHash = BitConverter.ToUInt32(ctx.CurrentInstruction!.Operand.Span);
                        sysCall = SyscallNames.TryGetValue(syscallHash, out var name)
                            ? name
                            : $"0x{syscallHash:X8}";
                    }
                    catch
                    {
                        sysCall = string.Empty;
                    }
                }

                try
                {
                    engine.ExecuteNext();
                    string stackTop = engine.ResultStack.Count > 0 ? engine.ResultStack.Peek().ToString() ?? string.Empty : string.Empty;
                    stackTop = Sanitize(stackTop);

                    _traceWriter!.WriteLine($"{tx.Hash},{block.Index},{step++},{scriptHash},{op},{sysCall},{engine.GasConsumed},{stackTop}");
                }
                catch
                {
                    break;
                }
            }
        }
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        Configure();
    }

    private static string Sanitize(string value)
    {
        // Trim noise for CSV safety.
        value = value.Replace(",", ";").Replace("\n", string.Empty).Replace("\r", string.Empty);
        if (value.Length > 64) value = value.Substring(0, 64) + "...";
        return value;
    }

    private void CloseWriters()
    {
        _blockWriter?.Flush();
        _blockWriter?.Close();

        _txWriter?.Flush();
        _txWriter?.Close();

        _traceWriter?.Flush();
        _traceWriter?.Close();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) CloseWriters();
    }
}
