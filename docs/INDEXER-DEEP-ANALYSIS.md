# Neo Indexer Deep Analysis (neo-indexer)

This document is a code-accurate deep dive into how `neo-indexer` captures data from a Neo node and persists it into Supabase (Postgres + Storage), and how that data is later queried via RPC.

It complements (not replaces) the higher-level design docs:
- `docs/ARCHITECTURE-neo-indexer-v2.md`
- `docs/DEPLOYMENT-SUPABASE-MAINNET.md`
- `docs/RPC-TRACES-API.md`

## 1. What this indexer actually does

At a high level the system captures **two classes of data**:

1) **Storage reads (state access) during block execution**
- Recorded at the storage engine layer (so it catches reads from any subsystem that goes through the store).
- Deduplicated so only the **first read of a unique `StorageKey` per block** is recorded.
- Includes contract metadata enrichment (`contract_hash`, `manifest_name`) and optional `tx_hash` attribution.

2) **Execution traces for persisted transactions**
- Captures traces at VM/diagnostic level: opcode execution, syscalls, contract calls, storage writes, notifications.
- Stored in partitioned Postgres tables keyed by `(block_index, tx_hash, order)` for idempotent upserts.

The “indexer” is therefore a **recorder + uploader**:
- Recorder: runs inside the Neo node process and captures state/trace data while blocks are persisted.
- Uploader: batches and upserts captured data into Supabase via REST API and/or direct Postgres, plus optional Storage uploads.

## 2. End-to-end data flow (block lifecycle)

```
Neo node
  ├─ Storage reads happen (DB lookups)
  │    └─ RecordingStoreProvider wrapper calls StateReadRecorder.Record(...)
  │         └─ BlockReadRecorder dedupes + stores the first read of each key
  │
  ├─ Transactions execute during block persist
  │    └─ TracingApplicationEngineProvider creates TracingApplicationEngine
  │         └─ TracingDiagnostic/TracingEngine record opcodes/syscalls/calls/writes/notifications
  │             └─ ExecutionTraceRecorder (per tx)
  │
  └─ Block committed event fires
       └─ BlockStateIndexerPlugin drains recorders for this block
            ├─ StateRecorderSupabase.TryUpload(BlockReadRecorder, mode)
            ├─ StateRecorderSupabase.TryQueueTraceUpload(block, ExecutionTraceRecorder) [per tx]
            └─ StateRecorderSupabase.TryQueueBlockStatsUpload(BlockStats)
                 └─ Background upload queue + concurrency throttles + retries
```

## 3. Recording storage reads

### 3.1 Storage wrapper: `RecordingStoreProvider`

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

### 3.2 Recorder scope + transaction attribution: `StateReadRecorder`

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

### 3.3 Data structure and dedupe behavior: `BlockReadRecorder`

- `src/Neo/Persistence/BlockReadRecorder.cs`

Key behaviors:
- Maintains a `HashSet<StorageKey>` to ensure **first-read-only** semantics.
- `TryAdd(...)` clones values (`StorageItem.Clone()`) so later mutations don’t change recorded state.
- Optional cap: `NEO_STATE_RECORDER__MAX_STORAGE_READS_PER_BLOCK` can limit unique keys per block to prevent runaway memory and huge inserts.
- Resolves contract metadata (`contract_hash`, `manifest_name`) using the ContractManagement native contract, with `StateReadRecorder.SuppressRecordingScope()` to avoid recursive recording during metadata lookups.

Trade-off:
- This captures the “initial observed value” of each key for the block, which is what you want for replay/analysis, but it is not a full key-history timeline.

## 4. Recording execution traces

### 4.1 Engine wiring: `TracingApplicationEngineProvider`

- `src/Neo/SmartContract/TracingApplicationEngineProvider.cs`

This provider replaces `ApplicationEngine.Provider` when enabled by the plugin. It:
- Starts a block read recorder scope on `TriggerType.OnPersist` when `StateReadRecorder.Enabled` is true.
- Creates/uses a per-block `BlockTraceRecorder` (keyed by `block.Index`) and per-tx `ExecutionTraceRecorder` (keyed by `tx.Hash`) when:
  - `trigger == TriggerType.Application`
  - `container is Transaction`
  - `persistingBlock != null`
- Wraps the engine’s diagnostic pipeline so tracing diagnostics and a read-attribution diagnostic run for each tx execution.

### 4.2 Trace aggregation: `ExecutionTraceRecorder`

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

### 4.3 Per-block aggregation: `BlockTraceRecorder`

- `src/Neo/Persistence/BlockTraceRecorder.cs`

Stores a `ConcurrentDictionary<UInt256, ExecutionTraceRecorder>` and can aggregate counts across all tx recorders.

## 5. Upload pipeline and backpressure

### 5.1 Entry point: `BlockStateIndexerPlugin`

- `src/Plugins/BlockStateIndexer/BlockStateIndexer.cs`

On `Blockchain.Committed`:
- drains the block read recorder (`DrainReadRecorder`)
- drains tx trace recorders (`DrainBlock`)
- decides what to upload based on:
  - plugin config (`BlockStateIndexer.json`)
  - environment recorder config (`StateRecorderSettings`)
  - minimum transaction count threshold for trace uploads (`MinTransactionCount`)

Uploads are queued rather than executed inline with block persistence.

### 5.2 Queue model: high vs low priority

- `src/Neo/Persistence/StateRecorderSupabase.UploadQueue.cs`

There is a bounded background queue with two lanes:
- **High priority**: block state (binary/json/csv), REST/PG block + reads upserts, block stats.
- **Low priority**: per-transaction trace uploads.

This prevents slow trace uploads from starving “index baseline” uploads (blocks + reads + stats).

When a lane is full, work is dropped and logged (with a periodic log cadence).

### 5.3 Concurrency controls: global throttle + trace-lane throttle

Within `StateRecorderSupabase`:
- `TraceUploadSemaphore` gates total concurrent HTTPS uploads (to avoid Supabase 429 throttling on mainnet).
- `TraceUploadLaneSemaphore` prevents low-priority trace uploads from using all upload slots.

Relevant env vars:
- `NEO_STATE_RECORDER__TRACE_UPLOAD_CONCURRENCY`
- `NEO_STATE_RECORDER__UPLOAD_QUEUE_WORKERS`
- `NEO_STATE_RECORDER__UPLOAD_QUEUE_CAPACITY`
- `NEO_STATE_RECORDER__TRACE_UPLOAD_QUEUE_CAPACITY`

### 5.4 Retry behavior

- `src/Neo/Persistence/StateRecorderSupabase.Retry.cs`

Each queued upload runs in `ExecuteWithRetryAsync`:
- 3 attempts
- exponential backoff (1s → 2s → 4s)
- logs failures
- does not throw to the caller (queue worker isolates failures)

This means the system prefers “keep indexing” over “fail the node”.

## 6. Supabase persistence model (schema + idempotency)

### 6.1 Core tables (reads)

Defined/adjusted in:
- `migrations/001_schema_alignment.sql`

Key tables:
- `blocks` keyed by `block_index` (upsert target)
- `contracts` keyed by `contract_id` (cache + upsert target)
- `storage_reads` (append-only by default)

Important detail: idempotent `storage_reads` upsert requires migration `012_storage_reads_idempotent_upsert.sql`. Without it, the uploader falls back to **delete+insert per block** to avoid duplicates.

### 6.2 Trace tables (partitioned, idempotent)

Defined in:
- `migrations/002_trace_tables.sql`

Partitioned by `block_index` (range). Primary keys enforce idempotency:
- `opcode_traces`: `(block_index, tx_hash, trace_order)`
- `syscall_traces`: `(block_index, tx_hash, trace_order)`
- `contract_calls`: `(block_index, tx_hash, trace_order)`
- `storage_writes`: `(block_index, tx_hash, write_order)`
- `notifications`: `(block_index, tx_hash, notification_order)`

Block-level aggregates:
- `block_stats` keyed by `block_index`

### 6.3 Partition management and pruning

See:
- `migrations/008_partition_management_security_definer.sql`

Provides SECURITY DEFINER functions for:
- creating partitions ahead of time (`ensure_trace_partitions`)
- pruning old partitions (`prune_trace_partitions`)

This is operationally important because mainnet grows indefinitely.

## 7. RPC query surface (reading traces from Supabase)

The RpcServer plugin offers trace-related endpoints that query Supabase directly:
- `src/Plugins/RpcServer/RpcServer.Traces.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Types.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Supabase.Settings.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Supabase.Client.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Supabase.Http.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Endpoints.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Endpoints.ContractCalls.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Endpoints.Stats.Syscalls.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Endpoints.Stats.OpCodes.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Endpoints.Stats.ContractCalls.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Parsing.BlockIdentifier.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Parsing.TraceRequestOptions.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Parsing.ContractCalls.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Parsing.Stats.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Parsing.Helpers.cs`

Key behaviors:
- Uses Supabase PostgREST reads for trace tables (and Supabase RPC functions for stats).
- Supports optional per-request limits/offsets with caps to protect Supabase.
- Supports an override key `NEO_RPC_TRACES__SUPABASE_KEY` (recommended for public RPC deployments so you can use an anon key + RLS).
- Has its own concurrency gate (`NEO_RPC_TRACES__MAX_CONCURRENCY`) to avoid stampedes.

## 8. Performance and scaling considerations

### 8.1 Memory growth risks
- `BlockReadRecorder` holds every unique key read for a block until commit. Very “hot” blocks can be huge.
  - Use `NEO_STATE_RECORDER__MAX_STORAGE_READS_PER_BLOCK` to cap.
- `ExecutionTraceRecorder` holds per-tx traces. If trace level is `All` and blocks are large, this is substantial.
  - Use `NEO_STATE_RECORDER__TRACE_LEVEL` to reduce.
  - Use `BlockStateIndexer.json` `MinTransactionCount` to skip traces for small blocks.

### 8.2 Write amplification
- REST API mode can involve:
  - upserts for blocks/contracts
  - batched inserts/upserts for reads and traces
  - optional stale-tail deletes when trimming is enabled
- Postgres direct mode is typically lower overhead (single transaction), but requires network connectivity to Postgres.

### 8.3 Backpressure vs completeness
The bounded queue can drop work under sustained Supabase slowness:
- high priority drops mean you may miss blocks/reads/stats
- low priority drops mean you may miss traces (by design preference)

If your goal is completeness:
- raise queue capacities
- raise worker count carefully (Supabase 429 risk)
- monitor drop counters via `StateRecorderSupabase.GetUploadQueueStats()`

## 9. Reliability and correctness notes

### 9.1 Idempotency and re-sync behavior
- `blocks` and trace tables are upserted by primary key, so re-sync is safe.
- `storage_reads` is only safely idempotent when migration `012` is applied; otherwise re-sync relies on delete+insert for each block.

### 9.2 Trace “stale row” trimming
When enabled (`NEO_STATE_RECORDER__TRACE_TRIM_STALE_ROWS=true`), the uploader deletes any rows with an order >= the latest uploaded count. This prevents old traces from remaining after:
- re-sync with different trace level
- partial uploads
- bugs that previously emitted more rows than current logic

This improves correctness at the cost of extra DELETE statements.

## 10. Security considerations

- Writes require Supabase service role key in most deployments (`NEO_STATE_RECORDER__SUPABASE_KEY`).
- Query-only RPC endpoints should not expose service role keys.
  - Prefer `NEO_RPC_TRACES__SUPABASE_KEY` with an anon key and rely on RLS policies from migrations.
- Partition management functions are SECURITY DEFINER and must remain admin-only.
