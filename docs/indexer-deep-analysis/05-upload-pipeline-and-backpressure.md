# 5. Upload pipeline and backpressure

## 5.1 Entry point: `BlockStateIndexerPlugin`

- `src/Plugins/BlockStateIndexer/BlockStateIndexer.cs`

On `Blockchain.Committed`:
- drains the block read recorder (`DrainReadRecorder`)
- drains tx trace recorders (`DrainBlock`)
- decides what to upload based on:
  - plugin config (`BlockStateIndexer.json`)
  - environment recorder config (`StateRecorderSettings`)
  - minimum transaction count threshold for trace uploads (`MinTransactionCount`) (tx results are still uploaded)

Uploads are queued rather than executed inline with block persistence.

## 5.2 Queue model: high vs low priority

- `src/Neo/Persistence/StateRecorderSupabase.UploadQueue.cs`

There is a bounded background queue with two lanes:
- **High priority**: block state (binary/json/csv), REST/PG block + reads upserts, block stats.
- **Low priority**: per-transaction uploads (tx result always; traces optionally).

This prevents slow trace uploads from starving “index baseline” uploads (blocks + reads + stats).

When a lane is full, work is dropped and logged (with a periodic log cadence).

## 5.3 Concurrency controls: global throttle + trace-lane throttle

Within `StateRecorderSupabase`:
- `TraceUploadSemaphore` gates total concurrent uploads (HTTP + direct Postgres) to avoid overloading Supabase on mainnet.
- `TraceUploadLaneSemaphore` prevents low-priority trace uploads from using all upload slots.

Relevant env vars:
- `NEO_STATE_RECORDER__TRACE_UPLOAD_CONCURRENCY`
- `NEO_STATE_RECORDER__UPLOAD_QUEUE_WORKERS`
- `NEO_STATE_RECORDER__UPLOAD_QUEUE_CAPACITY`
- `NEO_STATE_RECORDER__TRACE_UPLOAD_QUEUE_CAPACITY`

## 5.4 Retry behavior

- `src/Neo/Persistence/StateRecorderSupabase.Retry.cs`

Each queued upload runs in `ExecuteWithRetryAsync`:
- 3 attempts
- exponential backoff (1s → 2s → 4s)
- logs failures
- does not throw to the caller (queue worker isolates failures)

This means the system prefers “keep indexing” over “fail the node”.
