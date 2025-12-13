# Architecture Design Document: Neo Indexer Turnkey Stack v2.0

## 1. Overview

This document describes the technical architecture for extending the Neo Block State Indexer to capture complete execution traces including OpCodes, Syscalls, and contract interactions.

## 2. Key Neo Integration Points

### 2.1 IDiagnostic Interface (Official Hook)

Neo provides an official `IDiagnostic` interface for VM instrumentation:

```csharp
// src/Neo/SmartContract/IDiagnostic.cs
public interface IDiagnostic
{
    void Initialized(ApplicationEngine engine);
    void Disposed();
    void ContextLoaded(ExecutionContext context);      // Contract call start
    void ContextUnloaded(ExecutionContext context);    // Contract call end
    void PreExecuteInstruction(Instruction instruction);  // Before each opcode
    void PostExecuteInstruction(Instruction instruction); // After each opcode
}
```

### 2.2 Syscall Interception

Syscalls are handled via `OnSysCall` in ApplicationEngine:

```csharp
// ApplicationEngine.cs:264
protected virtual void OnSysCall(InteropDescriptor descriptor)
{
    // descriptor.Name contains human-readable syscall name
    // descriptor.Hash is the uint32 hash
    // descriptor.FixedPrice is the GAS cost
}
```

### 2.3 InteropDescriptor (Syscall Metadata)

```csharp
public static IReadOnlyDictionary<uint, InteropDescriptor> Services => services;
// Maps syscall hash -> { Name, Handler, FixedPrice, RequiredCallFlags }
```

## 3. Component Architecture

```
+------------------------------------------------------------------+
|                     Neo Node Process                              |
|  +------------------------------------------------------------+  |
|  |              BlockStateIndexer Plugin                       |  |
|  |                                                             |  |
|  |  +------------------+    +------------------+               |  |
|  |  | TracingDiagnostic|    | TracingAppEngine |               |  |
|  |  | (IDiagnostic)    |    | (extends AppEng) |               |  |
|  |  +--------+---------+    +--------+---------+               |  |
|  |           |                       |                         |  |
|  |           v                       v                         |  |
|  |  +------------------------------------------------+        |  |
|  |  |           ExecutionTraceRecorder               |        |  |
|  |  |  - OpCode traces                               |        |  |
|  |  |  - Syscall invocations                         |        |  |
|  |  |  - Contract call graph                         |        |  |
|  |  |  - Storage reads/writes                        |        |  |
|  |  |  - Notifications                               |        |  |
|  |  +------------------------+-----------------------+        |  |
|  |                           |                                 |  |
|  +---------------------------|----------------------------------+ |
|                              |                                    |
|                              v                                    |
|  +------------------------------------------------------------+  |
|  |              StateRecorderSupabase (Extended)               |  |
|  |  - UploadOpCodeTracesAsync()                                |  |
|  |  - UploadSyscallTracesAsync()                               |  |
|  |  - UploadContractCallsAsync()                               |  |
|  |  - UploadNotificationsAsync()                               |  |
|  +------------------------------------------------------------+  |
+------------------------------------------------------------------+
                              |
                              v
              +-------------------------------+
              |      Supabase Backend         |
              |  +-------------------------+  |
              |  | PostgreSQL (Partitioned)|  |
              |  | - opcode_traces         |  |
              |  | - syscall_traces        |  |
              |  | - contract_calls        |  |
              |  | - notifications         |  |
              |  | - storage_writes        |  |
              |  | - block_stats           |  |
              |  +-------------------------+  |
              +-------------------------------+
```

## 4. Core Components

### 4.1 TracingDiagnostic (New)

Implements `IDiagnostic` to capture OpCode execution:

```csharp
public class TracingDiagnostic : IDiagnostic
{
    private readonly ExecutionTraceRecorder _recorder;
    private ApplicationEngine _engine;
    private int _instructionOrder;

    public void Initialized(ApplicationEngine engine)
    {
        _engine = engine;
        _instructionOrder = 0;
    }

    public void PreExecuteInstruction(Instruction instruction)
    {
        _recorder.RecordOpCode(new OpCodeTrace
        {
            ContractHash = _engine.CurrentScriptHash,
            InstructionPointer = _engine.CurrentContext.InstructionPointer,
            OpCode = instruction.OpCode,
            Operand = instruction.Operand,
            GasBefore = _engine.FeeConsumed,
            StackDepth = _engine.CurrentContext.EvaluationStack.Count,
            Order = _instructionOrder++
        });
    }

    public void PostExecuteInstruction(Instruction instruction)
    {
        // Record GAS consumed by this instruction
        _recorder.UpdateOpCodeGas(_instructionOrder - 1, _engine.FeeConsumed);
    }

    public void ContextLoaded(ExecutionContext context)
    {
        // Contract call started - record call graph edge
        _recorder.RecordContractCall(new ContractCall
        {
            CallerHash = _engine.CallingScriptHash,
            CalleeHash = _engine.CurrentScriptHash,
            CallDepth = _engine.InvocationStack.Count
        });
    }

    public void ContextUnloaded(ExecutionContext context)
    {
        // Contract call ended
        _recorder.EndContractCall(_engine.CurrentScriptHash);
    }
}
```

### 4.2 TracingApplicationEngine (New)

Extends ApplicationEngine to intercept syscalls:

```csharp
public class TracingApplicationEngine : ApplicationEngine
{
    private readonly ExecutionTraceRecorder _recorder;
    private int _syscallOrder;

    protected override void OnSysCall(InteropDescriptor descriptor)
    {
        var gasBefore = FeeConsumed;

        // Record syscall invocation
        _recorder.RecordSyscall(new SyscallTrace
        {
            ContractHash = CurrentScriptHash,
            SyscallHash = descriptor.Hash.ToString("X8"),
            SyscallName = descriptor.Name,
            GasCost = descriptor.FixedPrice * ExecFeeFactor,
            Order = _syscallOrder++
        });

        // Execute the actual syscall
        base.OnSysCall(descriptor);
    }
}
```

### 4.3 ExecutionTraceRecorder (New)

Aggregates all trace data for a transaction:

```csharp
public class ExecutionTraceRecorder
{
    public uint BlockIndex { get; set; }
    public UInt256 TxHash { get; set; }

    public List<OpCodeTrace> OpCodeTraces { get; } = new();
    public List<SyscallTrace> SyscallTraces { get; } = new();
    public List<ContractCall> ContractCalls { get; } = new();
    public List<StorageWrite> StorageWrites { get; } = new();
    public List<NotificationEvent> Notifications { get; } = new();

    // Thread-safe recording methods
    public void RecordOpCode(OpCodeTrace trace) { ... }
    public void RecordSyscall(SyscallTrace trace) { ... }
    public void RecordContractCall(ContractCall call) { ... }
    public void RecordStorageWrite(StorageWrite write) { ... }
    public void RecordNotification(NotificationEvent notification) { ... }
}
```

### 4.4 IApplicationEngineProvider Integration

Neo allows custom ApplicationEngine via provider:

```csharp
// Register our tracing engine provider
ApplicationEngine.Provider = new TracingApplicationEngineProvider();

public class TracingApplicationEngineProvider : IApplicationEngineProvider
{
    public ApplicationEngine Create(
        TriggerType trigger, IVerifiable container, DataCache snapshot,
        Block persistingBlock, ProtocolSettings settings, long gas,
        IDiagnostic diagnostic, JumpTable jumpTable)
    {
        var recorder = new ExecutionTraceRecorder();
        var tracingDiagnostic = new TracingDiagnostic(recorder);

        return new TracingApplicationEngine(
            trigger, container, snapshot, persistingBlock,
            settings, gas, tracingDiagnostic, jumpTable, recorder);
    }
}
```

## 5. Data Models

### 5.1 OpCodeTrace

```csharp
public record OpCodeTrace
{
    public UInt160 ContractHash { get; init; }
    public int InstructionPointer { get; init; }
    public OpCode OpCode { get; init; }
    public string OpCodeName => OpCode.ToString();
    public ReadOnlyMemory<byte> Operand { get; init; }
    public long GasConsumed { get; init; }
    public int StackDepth { get; init; }
    public int Order { get; init; }
}
```

### 5.2 SyscallTrace

```csharp
public record SyscallTrace
{
    public UInt160 ContractHash { get; init; }
    public string SyscallHash { get; init; }
    public string SyscallName { get; init; }
    public long GasCost { get; init; }
    public int Order { get; init; }
}
```

### 5.3 ContractCall

```csharp
public record ContractCall
{
    public UInt160 CallerHash { get; init; }
    public UInt160 CalleeHash { get; init; }
    public string MethodName { get; init; }
    public int CallDepth { get; init; }
    public int Order { get; init; }
    public bool Success { get; set; }
    public long GasConsumed { get; set; }
}
```

## 6. Database Schema

### 6.1 Partitioning Strategy

Trace tables are partitioned by block_index ranges (100K blocks per partition):

```sql
-- Create parent table
CREATE TABLE opcode_traces (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    trace_order INTEGER NOT NULL,
    contract_hash TEXT NOT NULL,
    instruction_pointer INTEGER NOT NULL,
    opcode SMALLINT NOT NULL,
    opcode_name TEXT NOT NULL,
    operand_base64 TEXT,
    gas_consumed BIGINT NOT NULL,
    stack_depth INTEGER,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, trace_order)
) PARTITION BY RANGE (block_index);

-- Create partitions
CREATE TABLE opcode_traces_0_100000
    PARTITION OF opcode_traces
    FOR VALUES FROM (0) TO (100000);

CREATE TABLE opcode_traces_100000_200000
    PARTITION OF opcode_traces
    FOR VALUES FROM (100000) TO (200000);
```

### 6.2 Pruning Function

```sql
-- Drop old partitions for instant pruning
-- (see migrations/002_trace_tables.sql)
SELECT prune_old_partitions('opcode_traces', 1000000);
SELECT prune_old_partitions('syscall_traces', 1000000);
SELECT prune_old_partitions('contract_calls', 1000000);
SELECT prune_old_partitions('storage_writes', 1000000);
SELECT prune_old_partitions('notifications', 1000000);

-- Or prune all trace tables at once:
SELECT * FROM prune_trace_partitions(1000000);
```

On mainnet you should run pruning on a schedule (for example weekly) using a Supabase
scheduled SQL job. Choose `retention_blocks` based on your storage budget.
These functions are admin-only; execute them as `postgres` or with the service role key
via the Supabase SQL editor/cron. They are not exposed to anon/authenticated users.

Note: `storage_reads` is not partitioned. For retention on read capture, use:

```sql
-- Deletes old rows (not instant; may require VACUUM planning)
SELECT prune_storage_reads(1000000);
```

### 6.3 Automatic Partition Rotation

The migrations also define `ensure_trace_partitions(partition_size, lookahead_blocks)` which
creates missing partitions for all trace tables up to the current max `blocks.block_index`
plus a lookahead window. Schedule this in Supabase (or run manually) to keep the
`*_default` partitions empty on mainnet:

```sql
-- Create 100k‑block partitions up to current height + 200k
SELECT ensure_trace_partitions(100000, 200000);
```

## 7. Configuration

### 7.1 Extended Settings

```csharp
public sealed class StateRecorderSettings
{
    public bool Enabled { get; init; }
    public UploadMode Mode { get; init; } = UploadMode.Binary;

    // Supabase REST / Storage settings
    public string SupabaseUrl { get; init; } = string.Empty;
    public string SupabaseApiKey { get; init; } = string.Empty;
    public string SupabaseBucket { get; init; } = "block-state";
    public string SupabaseConnectionString { get; init; } = string.Empty;
    public bool UploadAuxFormats { get; init; }

    // Trace level flags (OpCodes, Syscalls, ContractCalls, Storage, Notifications)
    public ExecutionTraceLevel TraceLevel { get; init; } = ExecutionTraceLevel.All;

    // Optional safety cap for mainnet operation
    public int MaxStorageReadsPerBlock { get; init; }
}
```

Effective default: when `NEO_STATE_RECORDER__ENABLED=true` and Supabase `URL/KEY` are set but
`NEO_STATE_RECORDER__UPLOAD_MODE` is omitted, the recorder automatically defaults to `RestApi`
to ensure traces/reads are persisted in Supabase Postgres.

### 7.2 Environment Variables

```bash
# Enable recorder & Supabase REST mode
NEO_STATE_RECORDER__ENABLED=true
NEO_STATE_RECORDER__UPLOAD_MODE=RestApi
NEO_STATE_RECORDER__SUPABASE_URL=https://your-project.supabase.co
NEO_STATE_RECORDER__SUPABASE_KEY=your-service-key

# Optional: also upload JSON/CSV per block (default false)
NEO_STATE_RECORDER__UPLOAD_AUX_FORMATS=false

# Trace configuration (comma-separated flags)
NEO_STATE_RECORDER__TRACE_LEVEL=OpCodes,Syscalls,ContractCalls,Storage,Notifications

# Performance / throttling
NEO_STATE_RECORDER__TRACE_BATCH_SIZE=1000
# Caps concurrent HTTPS uploads to Supabase (snapshots, reads, traces, stats)
NEO_STATE_RECORDER__TRACE_UPLOAD_CONCURRENCY=4

# Optional: cap storage reads captured per block (0 = unlimited)
NEO_STATE_RECORDER__MAX_STORAGE_READS_PER_BLOCK=0

# Optional: RPC trace proxy guardrails (when Neo JSON-RPC is public)
# NEO_RPC_TRACES__SUPABASE_KEY=your-anon-key
# NEO_RPC_TRACES__MAX_CONCURRENCY=16
```

## 8. API Extensions

### 8.1 New RPC Methods

```csharp
// RpcServer.Traces.cs
[RpcMethod]
public JToken GetBlockTrace(JArray @params)
{
    uint blockIndex = (uint)@params[0].AsNumber();
    return GetTracesForBlock(blockIndex);
}

[RpcMethod]
public JToken GetTransactionTrace(JArray @params)
{
    UInt256 txHash = UInt256.Parse(@params[0].AsString());
    return GetTracesForTransaction(txHash);
}

[RpcMethod]
public JToken GetContractCalls(JArray @params)
{
    UInt160 contractHash = UInt160.Parse(@params[0].AsString());
    uint startBlock = (uint)@params[1].AsNumber();
    uint endBlock = (uint)@params[2].AsNumber();
    return GetContractCallsInRange(contractHash, startBlock, endBlock);
}
```

## 9. Frontend Components

### 9.1 Trace Browser

```typescript
// components/TraceBrowser.tsx
interface TraceBrowserProps {
    blockIndex?: number;
    txHash?: string;
}

const TraceBrowser: React.FC<TraceBrowserProps> = ({ blockIndex, txHash }) => {
    const { data: traces } = useQuery(['traces', blockIndex, txHash], fetchTraces);

    return (
        <div className="trace-browser">
            <OpCodeViewer traces={traces?.opcodes} />
            <SyscallTimeline traces={traces?.syscalls} />
            <CallGraph calls={traces?.contractCalls} />
            <StorageDiff writes={traces?.storageWrites} />
        </div>
    );
};
```

### 9.2 OpCode Viewer (Debugger Style)

```typescript
// components/OpCodeViewer.tsx
const OpCodeViewer: React.FC<{ traces: OpCodeTrace[] }> = ({ traces }) => {
    return (
        <table className="opcode-table font-mono text-sm">
            <thead>
                <tr>
                    <th>IP</th>
                    <th>OpCode</th>
                    <th>Operand</th>
                    <th>GAS</th>
                    <th>Stack</th>
                </tr>
            </thead>
            <tbody>
                {traces.map((trace, i) => (
                    <tr key={i} className={getOpCodeClass(trace.opcode)}>
                        <td>{trace.instructionPointer}</td>
                        <td>{trace.opcodeName}</td>
                        <td>{formatOperand(trace.operand)}</td>
                        <td>{formatGas(trace.gasConsumed)}</td>
                        <td>{trace.stackDepth}</td>
                    </tr>
                ))}
            </tbody>
        </table>
    );
};
```

## 10. Performance Considerations

### 10.1 Async Upload Pipeline

```
Block Commit -> Trace Collection -> Background Queue -> Batch Upload
                                          |
                                    (Non-blocking)
```

### 10.2 Memory Management

- Use object pooling for trace records
- Limit in-memory trace buffer size
- Flush to database in batches

### 10.3 Database Optimization

- Partitioned tables for fast pruning
- Composite indexes on (block_index, contract_hash)
- BRIN indexes for time-series queries

## 11. Implementation Priority

1. **Phase 1**: TracingDiagnostic + OpCode capture
2. **Phase 2**: TracingApplicationEngine + Syscall capture
3. **Phase 3**: Contract call graph + Storage writes
4. **Phase 4**: Database partitioning + Pruning
5. **Phase 5**: RPC extensions + Frontend UI

## 12. Risk Mitigation

| Risk               | Mitigation                              |
| ------------------ | --------------------------------------- |
| Performance impact | Configurable trace levels, async upload |
| Memory pressure    | Object pooling, batch processing        |
| Storage growth     | Partitioning, automatic pruning         |
| API compatibility  | Separate RPC namespace for traces       |
