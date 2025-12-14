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
- The tracer-only old/new value reads and contract metadata lookup (contract id → contract hash) are wrapped in `StateReadRecorder.SuppressRecordingScope()` to avoid polluting `storage_reads`.
  - See `src/Neo/SmartContract/TracingApplicationEngine.Storage.cs`
- Deletes are recorded with an explicit `is_delete` flag (so deletes can be distinguished from writes that set an empty byte array value).

Syscall tracing notes:
- Implemented by overriding `ApplicationEngine.OnSysCall` in `src/Neo/SmartContract/TracingApplicationEngine.Syscalls.cs`.
- The recorded `gasCost` is computed from the engine’s `FeeConsumed` delta across the syscall, so it includes any dynamic fees charged inside the syscall handler.

Opcode tracing notes:
- Implemented by `TracingDiagnostic` (`src/Neo/SmartContract/TracingDiagnostic.OpCodes.cs`).
- `gasConsumed` is the opcode fee (`OpCodePriceTable * ExecFeeFactor`). Syscall fees are recorded separately in `syscall_traces`.

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
- `HasTraces` indicates whether any per-opcode/syscall/call/write/notification traces were captured (tx results can still be uploaded even if trace capture is disabled).
- `Get*Traces()` returns a snapshot and sorts only if needed (fast-path if already ordered).
- `GetStats()` provides per-transaction aggregated counts (used to build block-level aggregates).
- Per-transaction “final result” fields are filled from the engine on disposal:
  - `VmState` (`HALT` / `FAULT`)
  - `TotalGasConsumed`
  - `FaultException` (best-effort string)
  - `ResultStackJson` (best-effort JSON array)

## 4.3 Per-block aggregation: `BlockTraceRecorder`

- `src/Neo/Persistence/BlockTraceRecorder.cs`

Stores a `ConcurrentDictionary<UInt256, ExecutionTraceRecorder>` and can aggregate counts across all tx recorders.
