# 9. Reliability and correctness notes

## 9.1 Idempotency and re-sync behavior
- `blocks` and trace tables are upserted by primary key, so re-sync is safe.
- `storage_reads` is only safely idempotent when migration `012` is applied; otherwise re-sync relies on delete+insert for each block.

## 9.2 Trace “stale row” trimming
When enabled (`NEO_STATE_RECORDER__TRACE_TRIM_STALE_ROWS=true`), the uploader deletes any rows with an order >= the latest uploaded count. This prevents old traces from remaining after:
- re-sync with different trace level
- partial uploads
- bugs that previously emitted more rows than current logic

This improves correctness at the cost of extra DELETE statements.

## 9.3 Chain reorganizations (reorgs) and “orphan” rows
Neo can (rarely) reorganize the tip of the chain. When this happens, some block heights are **re-persisted** with a new block hash and a different transaction set.

What is safe by construction:
- `blocks` is keyed by `block_index` and uses upserts, so the `block_hash` row can be updated to the new canonical block.
- `block_stats` is keyed by `block_index` and is also upserted.

What can become stale without extra cleanup:
- Trace tables are keyed by `(block_index, tx_hash, order)`. If a reorg replaces the tx set at a height, rows for tx hashes that no longer exist at that height can remain in:
  - `opcode_traces`, `syscall_traces`, `contract_calls`, `storage_writes`, `notifications`
- `transaction_results` is keyed by `(block_index, tx_hash)`, so tx outcome rows from the old block can remain at the same height when the tx set changes.
- With migration `012` enabled, `storage_reads` uses a unique key on `(block_index, contract_id, key_base64)`. That makes uploads idempotent, but it also means keys that were read by the **old** block at that height can remain if the **new** block never reads them (because there is no conflicting row to overwrite).

What this fork does to mitigate reorg orphans (when enabled):
- `StateRecorderSupabase` tracks a best-effort **canonical block hash** per `block_index` (in-process) and wraps queued uploads in a guard:
  - if the queued work’s `expectedBlockHash` is no longer canonical, the upload is skipped
  - if a reorg cleanup is in-flight for that height, the upload waits for it
- When `NEO_STATE_RECORDER__TRACE_TRIM_STALE_ROWS=true` and a block hash replacement is observed at the same height, the uploader schedules a **high priority** reorg cleanup that deletes all per-block rows for that height (reads + traces + tx results) before re-uploading.

Code pointers:
- Canonical hash tracking + canonical-only execution: `src/Neo/Persistence/StateRecorderSupabase.ReorgGuard.*.cs`
- Reorg cleanup barrier + queueing: `src/Neo/Persistence/StateRecorderSupabase.ReorgCleanup.Barriers.cs`, `src/Neo/Persistence/StateRecorderSupabase.ReorgCleanup.Queueing.cs`
- Delete implementations: `src/Neo/Persistence/StateRecorderSupabase.ReorgCleanup.RestApi.cs`, `src/Neo/Persistence/StateRecorderSupabase.ReorgCleanup.Postgres.cs`
- Detection + scheduling: `src/Neo/Persistence/StateRecorderSupabase.Dispatch.cs`

Operational implications:
- For “append-only analytics”, stale rows are usually acceptable (they only affect a small reorg window), and you can leave trimming disabled.
- For “exact state reconstruction / replay correctness”, enable trimming so reorgs trigger per-height cleanup (delete by `block_index` and re-upload).
  - Trace table DELETE policies exist for service role deployments (see migration `009_trace_delete_policies_and_indexes.sql`).
