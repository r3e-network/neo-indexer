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
            ├─ StateRecorderSupabase.TryQueueTraceUpload(blockIndex, blockHash, ExecutionTraceRecorder) [per tx]
            └─ StateRecorderSupabase.TryQueueBlockStatsUpload(BlockStats, blockHash)
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

Trace/write interactions:
- Storage write tracing (`System.Storage.Put/Delete`) needs to read the old/new values and resolve contract metadata.
- The tracer-only contract metadata lookup (contract id → contract hash) is wrapped in `StateReadRecorder.SuppressRecordingScope()` to avoid polluting `storage_reads`.
  - See `src/Neo/SmartContract/TracingApplicationEngine.Storage.cs`

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
  - optional per-block deletes when a tip reorg is detected (see 9.3)
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

### 9.3 Chain reorganizations (reorgs) and “orphan” rows
Neo can (rarely) reorganize the tip of the chain. When this happens, some block heights are **re-persisted** with a new block hash and a different transaction set.

What is safe by construction:
- `blocks` is keyed by `block_index` and uses upserts, so the `block_hash` row can be updated to the new canonical block.
- `block_stats` is keyed by `block_index` and is also upserted.

What can become stale without extra cleanup:
- Trace tables are keyed by `(block_index, tx_hash, order)`. If a reorg replaces the tx set at a height, rows for tx hashes that no longer exist at that height can remain in:
  - `opcode_traces`, `syscall_traces`, `contract_calls`, `storage_writes`, `notifications`
- With migration `012` enabled, `storage_reads` uses a unique key on `(block_index, contract_id, key_base64)`. That makes uploads idempotent, but it also means keys that were read by the **old** block at that height can remain if the **new** block never reads them (because there is no conflicting row to overwrite).

What this fork does to mitigate reorg orphans (when enabled):
- `StateRecorderSupabase` tracks a best-effort **canonical block hash** per `block_index` (in-process) and wraps queued uploads in a guard:
  - if the queued work’s `expectedBlockHash` is no longer canonical, the upload is skipped
  - if a reorg cleanup is in-flight for that height, the upload waits for it
- When `NEO_STATE_RECORDER__TRACE_TRIM_STALE_ROWS=true` and a block hash replacement is observed at the same height, the uploader schedules a **high priority** reorg cleanup that deletes all per-block rows for that height (reads + traces) before re-uploading.

Code pointers:
- Canonical hash tracking + canonical-only execution: `src/Neo/Persistence/StateRecorderSupabase.ReorgGuard.*.cs`
- Reorg cleanup barrier + queueing: `src/Neo/Persistence/StateRecorderSupabase.ReorgCleanup.Barriers.cs`, `src/Neo/Persistence/StateRecorderSupabase.ReorgCleanup.Queueing.cs`
- Delete implementations: `src/Neo/Persistence/StateRecorderSupabase.ReorgCleanup.RestApi.cs`, `src/Neo/Persistence/StateRecorderSupabase.ReorgCleanup.Postgres.cs`
- Detection + scheduling: `src/Neo/Persistence/StateRecorderSupabase.Dispatch.cs`

Operational implications:
- For “append-only analytics”, stale rows are usually acceptable (they only affect a small reorg window), and you can leave trimming disabled.
- For “exact state reconstruction / replay correctness”, enable trimming so reorgs trigger per-height cleanup (delete by `block_index` and re-upload).
  - Trace table DELETE policies exist for service role deployments (see migration `009_trace_delete_policies_and_indexes.sql`).

## 10. Security considerations

- Writes require Supabase service role key in most deployments (`NEO_STATE_RECORDER__SUPABASE_KEY`).
- Query-only RPC endpoints should not expose service role keys.
  - Prefer `NEO_RPC_TRACES__SUPABASE_KEY` with an anon key and rely on RLS policies from migrations.
- Partition management functions are SECURITY DEFINER and must remain admin-only.

## 11. Block state file exports (Supabase Storage)

In addition to writing rows into Postgres (via REST API or direct Postgres), the recorder can upload **per-block snapshot files** to Supabase Storage.

Where this is implemented:
- `src/Neo/Persistence/StateRecorderSupabase.Dispatch.Binary.cs`
- `src/Neo/Persistence/StateRecorderSupabase.BinaryUpload.*.cs`
- `src/Neo/Persistence/StateRecorderSupabase.JsonCsvUpload.*.cs`

### 11.1 Binary snapshots (`.bin`, NSBR format)

When the effective upload mode includes `Binary` and Supabase Storage is configured, the recorder uploads:
- `block-{blockIndex}.bin` to bucket `NEO_STATE_RECORDER__SUPABASE_BUCKET` (default: `block-state`)

It uses an HTTP `PUT` with `x-upsert=true` so re-syncs overwrite existing files:
- `src/Neo/Persistence/StateRecorderSupabase.BinaryUpload.cs`

Binary format is defined by the writer:
- `src/Neo/Persistence/StateRecorderSupabase.BinaryUpload.PayloadBuilders.Write.cs`

Format:
- Header: `[Magic "NSBR": 4 bytes] [Version: uint16] [BlockIndex: uint32] [EntryCount: int32]`
- Entries: `[ContractHash: 20 bytes] [KeyLen: uint16] [KeyBytes] [ValueLen: int32] [ValueBytes] [ReadOrder: int32]`

Important nuance: `KeyBytes` is a serialized `StorageKey`:
- `[ContractId: int32 little-endian] + [StorageKey.Key bytes]`

This is the format consumed by `StateReplay` (see section 12).

### 11.2 Optional JSON/CSV snapshots (`.json` / `.csv`)

When `NEO_STATE_RECORDER__UPLOAD_AUX_FORMATS=true`, the uploader additionally writes:
- `block-{blockIndex}.json`
- `block-{blockIndex}.csv`

The JSON format includes rich per-read metadata (contract id/hash, manifest name, tx hash attribution, source) and is intended for debugging/inspection:
- `src/Neo/Persistence/StateRecorderSupabase.JsonCsvUpload.PayloadBuilders.Json.cs`

Operationally, these formats are disabled by default because they create a large number of files and can be sizeable on “hot” blocks.

### 11.3 Empty-read blocks

If a block produced **zero recorded reads**, the plugin still upserts the `blocks` row (so the UI can find the block), but it avoids binary snapshot uploads to prevent file explosion:
- `src/Plugins/BlockStateIndexer/BlockStateIndexerPlugin.Handlers.Committed.StorageReads.cs`

## 12. StateReplay plugin (replaying blocks against captured state)

The `StateReplay` plugin is a debugging tool: it can replay a given block using a captured key/value snapshot (from a file or Supabase) to help diagnose determinism/state issues.

Where this is implemented:
- `src/Plugins/StateReplay/StateReplayPlugin.Replay.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Commands.BlockState.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Commands.Binary.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Commands.Supabase.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Supabase.Replay.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Supabase.Download.cs`
- `src/Plugins/StateReplay/BinaryFormatReader.cs`

### 12.1 Replay algorithm

`ReplayBlock(Block block, StoreCache snapshot)` follows Neo’s persistence lifecycle at a high level:
1. Runs native `OnPersist` via a syscall script.
2. Executes each transaction script:
   - If `HALT`, commits the snapshot changes.
   - If `FAULT`, discards changes and resets the snapshot to the original base (by cloning).
3. Runs native `PostPersist` via a syscall script.

This is a deliberately “pure” replay: it does not depend on the node’s live storage, only on the provided snapshot and the block’s transactions.

### 12.2 Snapshot inputs

StateReplay supports multiple snapshot sources:

- **JSON snapshot file** (`replay block-state <file>`):
  - Expects the same “block JSON export” shape that `StateRecorderSupabase` can upload (`block-<index>.json`)
  - Reads base64 `key`/`value` and casts `key` bytes into a `StorageKey`

- **Binary snapshot file** (`replay block-binary <file>`):
  - Reads NSBR (`block-<index>.bin`) via `BinaryFormatReader`
  - Loads entries into a `MemoryStore` snapshot

- **Supabase PostgREST** (`replay supabase <blockIndex>`):
  - Pages through `storage_reads` ordered by `read_order` and reconstructs `StorageKey { Id, Key }`
  - Useful when you didn’t upload storage files but did persist `storage_reads`

- **Supabase Storage download** (`replay download <blockIndex>`):
  - Downloads `block-<index>.bin` into a local cache directory (`StateReplay.json` `CacheDirectory`)

### 12.3 Current limitations

`replay compare <snapshotFile>` replays the block against the snapshot and produces a *read coverage* report (hit/miss keys at the store layer):
- `src/Plugins/StateReplay/StateReplayPlugin.Commands.Compare.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Compare.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Compare.Snapshot.cs`
- `src/Plugins/StateReplay/ReadCapturingStoreSnapshot.cs`

This is useful to answer questions like:
- “Did the replay attempt to read a key that is missing from my snapshot?”
- “How much of the snapshot was actually used?”
- “Did the replay read keys that were not present in the snapshot (e.g., keys created during replay)?”

It still does **not** perform a full “live execution vs replay” diff (events, ordering, VM state transitions, storage *values*, etc.); it is primarily a snapshot *coverage* and missing-data diagnostic.

## 13. Enablement + mode matrix (plugin config vs env)

There are two layers of configuration that jointly determine whether data is captured and where it goes:

1) **Plugin config** (`src/Plugins/BlockStateIndexer/BlockStateIndexer.json`)
- `Enabled`: gates whether `Blockchain.Committed` handling is active
- `UploadMode`: what the plugin *intends* to upload (Binary / RestApi / Both / Postgres)
- `MinTransactionCount`: trace upload gate (see below)

2) **Environment variables** (`StateRecorderSettings`, prefix `NEO_STATE_RECORDER__`)
- `ENABLED`: gates whether recorders are activated
- `SUPABASE_URL` + `SUPABASE_KEY`: enable REST/Storage uploads (`UploadEnabled`)
- `SUPABASE_CONNECTION_STRING`: enables direct Postgres uploads
- `UPLOAD_MODE`: limits which upload modes are allowed at runtime
- plus operational knobs like `TRACE_LEVEL`, `TRACE_UPLOAD_CONCURRENCY`, `UPLOAD_QUEUE_*`, `MAX_STORAGE_READS_PER_BLOCK`, etc.

### 13.1 What must be true to capture traces/reads

`BlockStateIndexer` registers the tracing provider only when BOTH are enabled:
- `Settings.Default.Enabled` (plugin JSON)
- `StateRecorderSettings.Current.Enabled` (env)

See:
- `src/Plugins/BlockStateIndexer/BlockStateIndexerPlugin.OnSystemLoaded.cs`

### 13.2 Upload allow-listing (Binary vs DB)

On each committed block, the plugin computes:
- `allowBinaryUploads = (plugin allows Binary) AND (env allows Binary)`
- `allowRestApiUploads = (plugin allows RestApi/Postgres) AND (env allows RestApi/Postgres)`

See:
- `src/Plugins/BlockStateIndexer/BlockStateIndexerPlugin.Handlers.Committed.UploadModes.cs`

### 13.3 `MinTransactionCount` behavior

`MinTransactionCount` applies to **trace uploads** (not to read recording):
- If `block.Transactions.Length < MinTransactionCount`, traces are skipped (reads may still be uploaded).

See:
- `src/Plugins/BlockStateIndexer/BlockStateIndexerPlugin.Handlers.Committed.Traces.cs`

### 13.4 REST API vs Postgres fallback behavior

Within `StateRecorderSupabase`, “database upload” chooses REST vs direct Postgres based on the effective mode and available credentials:
- `RestApi/Both`: prefers REST when URL/key exist, otherwise falls back to Postgres when a connection string exists.
- `Postgres`: prefers Postgres when a connection string exists, otherwise falls back to REST when URL/key exist.

See:
- `src/Neo/Persistence/StateRecorderSupabase.Dispatch.Database.cs`
