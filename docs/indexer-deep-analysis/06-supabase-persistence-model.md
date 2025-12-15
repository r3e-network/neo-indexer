# 6. Supabase persistence model (schema + idempotency)

## 6.1 Core tables (reads)

Defined/adjusted in:
- `migrations/001_schema_alignment.sql`

Key tables:
- `blocks` keyed by `block_index` (upsert target)
- `contracts` keyed by `contract_id` (cache + upsert target)
- `storage_reads` (append-only by default)

Important detail: idempotent `storage_reads` upsert requires migration `012_storage_reads_idempotent_upsert.sql`. Without it, the uploader falls back to **delete+insert per block** to avoid duplicates.

## 6.2 Trace tables (partitioned, idempotent)

Defined in:
- `migrations/002_trace_tables.sql`
- `migrations/014_transaction_results.sql`
- `migrations/018_storage_writes_is_delete.sql`
- `migrations/019_runtime_logs.sql`

Partitioned by `block_index` (range). Primary keys enforce idempotency:
- `opcode_traces`: `(block_index, tx_hash, trace_order)`
- `syscall_traces`: `(block_index, tx_hash, trace_order)`
- `contract_calls`: `(block_index, tx_hash, trace_order)`
- `storage_writes`: `(block_index, tx_hash, write_order)`
- `notifications`: `(block_index, tx_hash, notification_order)`
- `runtime_logs`: `(block_index, tx_hash, log_order)`
- `transaction_results`: `(block_index, tx_hash)`

Block-level aggregates:
- `block_stats` keyed by `block_index`

Note: `storage_writes` includes an `is_delete` boolean to disambiguate deletes from writes that set an empty byte array (`migrations/018_storage_writes_is_delete.sql`).
Note: `transaction_results` includes `log_count` (`migrations/020_transaction_results_log_count.sql`) to support fast log volume queries without joining `runtime_logs`.
Note: `block_stats` includes `log_count` (`migrations/021_block_stats_log_count.sql`) to support fast per-block dashboards without scanning `runtime_logs`.

## 6.3 Partition management and pruning

See:
- `migrations/008_partition_management_security_definer.sql`
- `migrations/029_partition_management_runtime_logs.sql`

Provides SECURITY DEFINER functions for:
- creating partitions ahead of time (`ensure_trace_partitions`)
- pruning old partitions (`prune_trace_partitions`)

This is operationally important because mainnet grows indefinitely.

Note: `ensure_trace_partitions` and `prune_trace_partitions` are extended over time (via migrations) to manage partitions for newer trace tables like `transaction_results` and `runtime_logs`.
