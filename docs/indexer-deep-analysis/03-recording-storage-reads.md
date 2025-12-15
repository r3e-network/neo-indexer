# 3. Recording storage reads

## 3.1 Storage wrapper: `RecordingStoreProvider`

The storage read hook does **not** require patching Neo core logic. Instead, it wraps the underlying `IStore` provider:

- `src/Plugins/BlockStateIndexer/RecordingStoreProvider.cs`
- `src/Plugins/BlockStateIndexer/RecordingStoreProvider.RecordingStore.cs`
- `src/Plugins/BlockStateIndexer/RecordingStoreProvider.RecordingStoreSnapshot.cs`

`RecordingStore`/`RecordingStoreSnapshot` intercept `TryGet(...)`, `Contains(...)`, and `Find(...)` calls. When `StateReadRecorder.IsRecording` is true, they convert byte keys to `StorageKey` and values to `StorageItem`, then call:

`StateReadRecorder.Record(this, storageKey, storageItem, source)`

Key properties:
- **Low overhead when disabled**: wrappers do nothing unless `StateReadRecorder.IsRecording` is true.
- **`Contains(...)` becomes `TryGet(...)` when recording**: this is intentional so the recorder captures the value that the code path depends on.
- **Find enumeration records each returned row**: still gated by `IsRecording`.
- **Best-effort read attribution**: when a read happens inside a syscall handler, the indexer records the syscall name into `storage_reads.source` (e.g., `System.Storage.Get`, `System.Storage.Find`). Otherwise it falls back to the underlying store operation name (`TryGet`, `Contains`, `Find`).

## 3.2 Recorder scope + transaction attribution: `StateReadRecorder`

- `src/Neo/Persistence/StateReadRecorder.cs`

Important mechanics:
- Uses `AsyncLocal` to keep a per-async-context:
  - current `BlockReadRecorder`
  - current transaction hash (optional)
  - suppression counter to avoid recursion
- `StateReadRecorder.TryBegin(Block)` returns a `BlockReadRecorderScope` that:
  - installs `StateReadRecorder.Current = recorder`
  - clears `TransactionHash` at block scope start
  - restores previous state on dispose

Transaction hash attribution is best-effort:
- `StateReadRecorder.BeginTransaction(UInt256? txHash)` sets an `AsyncLocal` `TransactionHash`.
- RecordingStore passes the tx hash only indirectly (via `StateReadRecorder.Record(...)`), so correct attribution depends on reads happening on the same async context that was annotated.

## 3.3 Data structure and dedupe behavior: `BlockReadRecorder`

- `src/Neo/Persistence/BlockReadRecorder.cs`

Key behaviors:
- Maintains a `HashSet<StorageKey>` to ensure **first-read-only** semantics.
- `TryAdd(...)` clones values (`StorageItem.Clone()`) so later mutations don’t change recorded state.
- Optional cap: `NEO_STATE_RECORDER__MAX_STORAGE_READS_PER_BLOCK` can limit unique keys per block to prevent runaway memory and huge inserts.
- Resolves contract metadata (contract id → `contract_hash`, `manifest_name`) using the ContractManagement native contract, with `StateReadRecorder.SuppressRecordingScope()` to avoid recursive recording during metadata lookups.
  - The `storage_reads` table stores `contract_id` (not `contract_hash`); metadata is upserted into the `contracts` reference table and can be joined in queries (or embedded via PostgREST).

Trade-off:
- This captures the “initial observed value” of each key for the block, which is what you want for replay/analysis, but it is not a full key-history timeline.
