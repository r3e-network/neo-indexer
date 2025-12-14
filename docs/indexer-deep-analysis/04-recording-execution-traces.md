# 4. Recording execution traces

## 4.1 Engine wiring: `TracingApplicationEngineProvider`

- `src/Neo/SmartContract/TracingApplicationEngineProvider.cs`

This provider replaces `ApplicationEngine.Provider` when enabled by the plugin. It:
- Starts a block read recorder scope on `TriggerType.OnPersist` when `StateReadRecorder.Enabled` is true.
- Creates/uses a per-block `BlockTraceRecorder` (keyed by `block.Index`) and per-tx `ExecutionTraceRecorder` (keyed by `tx.Hash`) when:
  - `trigger == TriggerType.Application`
  - `container is Transaction`
  - `persistingBlock != null`
- Wraps the engine’s diagnostic pipeline so tracing diagnostics and a read-attribution diagnostic run for each tx execution.

Trace/write interactions:
- Storage write tracing (`System.Storage.Put/Delete`) needs to read the old/new values and resolve contract metadata.
- The tracer-only contract metadata lookup (contract id → contract hash) is wrapped in `StateReadRecorder.SuppressRecordingScope()` to avoid polluting `storage_reads`.
  - See `src/Neo/SmartContract/TracingApplicationEngine.Storage.cs`

## 4.2 Trace aggregation: `ExecutionTraceRecorder`

- `src/Neo/Persistence/ExecutionTraceRecorder.Core.cs`
- `src/Neo/Persistence/ExecutionTraceRecorder.Accessors.OpCodesSyscalls.cs`
- `src/Neo/Persistence/ExecutionTraceRecorder.Accessors.CallsWrites.cs`
- `src/Neo/Persistence/ExecutionTraceRecorder.Accessors.Notifications.cs`
- `src/Neo/Persistence/ExecutionTraceRecorder.Recording.*.cs`
- `src/Neo/Persistence/ExecutionTraceRecorder.Stats.cs`

Characteristics:
- Thread-safe queues (`ConcurrentQueue<T>`) for trace categories.
- Per-category atomic counters and per-category “order” counters.
- `HasTraces` is used to avoid enqueuing empty trace uploads.
- `Get*Traces()` returns a snapshot and sorts only if needed (fast-path if already ordered).
- `GetStats()` provides per-transaction aggregated counts (used to build block-level aggregates).

## 4.3 Per-block aggregation: `BlockTraceRecorder`

- `src/Neo/Persistence/BlockTraceRecorder.cs`

Stores a `ConcurrentDictionary<UInt256, ExecutionTraceRecorder>` and can aggregate counts across all tx recorders.
