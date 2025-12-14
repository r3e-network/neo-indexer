# Supabase Mainnet Deployment Checklist

This checklist walks through deploying the Neo Block State Indexer v2 on **mainnet** with a **Supabase‑only** backend.  
The indexer writes using the **service role** key; the frontend reads directly from Supabase using the **anon** key.

## 1. Create / Prepare Supabase Project

1. Create a Supabase project in the dashboard.
2. Note these values from **Settings → API**:
   - Project URL: `https://<project>.supabase.co`
   - **Service role** key (write access, keep secret)
   - **Anon** key (public read access)

## 2. Apply Database Migrations

Run the SQL files in order in the Supabase SQL editor:

1. `migrations/001_schema_alignment.sql`
2. `migrations/002_trace_tables.sql`
3. `migrations/003_syscall_names.sql`
4. `migrations/004_public_read_policies.sql`
5. `migrations/005_stats_functions.sql`
6. `migrations/006_contract_call_stats.sql`
7. `migrations/007_stats_range_caps.sql`
8. `migrations/008_partition_management_security_definer.sql`
9. `migrations/009_trace_delete_policies_and_indexes.sql`
10. `migrations/010_prune_storage_reads.sql`
11. `migrations/011_prune_storage_reads_batched.sql`
12. `migrations/012_storage_reads_idempotent_upsert.sql`
13. `migrations/013_storage_reads_update_policy.sql`
14. `migrations/014_transaction_results.sql`
15. `migrations/015_transaction_results_delete_policy.sql`
16. `migrations/016_partition_management_transaction_results_support.sql`
17. `migrations/017_prune_trace_partitions_include_transaction_results.sql`
18. `migrations/018_storage_writes_is_delete.sql`
19. `migrations/019_runtime_logs.sql`
20. `migrations/020_transaction_results_log_count.sql`
21. `migrations/021_block_stats_log_count.sql`
22. `migrations/022_runtime_log_stats.sql`
23. `migrations/023_block_stats_rpc.sql`
24. `migrations/024_notification_stats.sql`
25. `migrations/025_storage_write_stats.sql`
26. `migrations/026_storage_read_stats.sql`

Notes:
- `002_trace_tables.sql` sets up range partitions and locks down partition management RPCs.
- `003_syscall_names.sql` seeds the `syscall_names` reference table (safe to re-run; uses upsert).
- `004_public_read_policies.sql` enables RLS + public read policies, and restricts writes to service role only.
- `009_trace_delete_policies_and_indexes.sql` adds service-role DELETE policies for trace tables and an index for method name filters.
- `010_prune_storage_reads.sql` adds `prune_storage_reads(retention_blocks)` for retention on the non-partitioned `storage_reads` table.
- `011_prune_storage_reads_batched.sql` adds optional batched pruning (`batch_size`, `max_batches`) and makes the 1-arg function use batching internally.
- `012_storage_reads_idempotent_upsert.sql` adds a unique index on `(block_index, contract_id, key_base64)` so REST/Postgres uploads can upsert reads without per-block deletes.
- `013_storage_reads_update_policy.sql` allows service_role UPDATE on `storage_reads` so PostgREST upserts can merge duplicates during resync.
- `014_transaction_results.sql` adds `transaction_results` (per-transaction VM state + gas + result stack) for SQL analytics and the trace RPC API.
- `015_transaction_results_delete_policy.sql` allows service_role DELETE on `transaction_results` so reorg cleanup can remove stale tx outcomes by height.
- `016_partition_management_transaction_results_support.sql` extends the partition helper allowlists (SECURITY DEFINER functions) so `ensure_trace_partitions` can manage `transaction_results` too.
- `017_prune_trace_partitions_include_transaction_results.sql` makes `prune_trace_partitions(retention_blocks)` also prune old `transaction_results_*` partitions.
- `018_storage_writes_is_delete.sql` adds an explicit `is_delete` flag to `storage_writes` to disambiguate deletes from empty-value writes.
- `019_runtime_logs.sql` adds the partitioned `runtime_logs` trace table for `System.Runtime.Log` and extends the partition management/pruning helper allowlists.
- `020_transaction_results_log_count.sql` adds `log_count` to `transaction_results` so log volume analytics can be done without joining `runtime_logs`.
- `021_block_stats_log_count.sql` adds `log_count` to `block_stats` for fast per-block dashboards without scanning `runtime_logs`.
- `022_runtime_log_stats.sql` adds `get_runtime_log_stats(...)` so public RPC endpoints can expose bounded log analytics (`getlogstats`).
- `023_block_stats_rpc.sql` adds `get_block_stats(...)` so public RPC endpoints can fetch bounded per-block aggregates (`getblockstats`).
- `024_notification_stats.sql` adds `get_notification_stats(...)` so public RPC endpoints can expose bounded event analytics (`getnotificationstats`).
- `025_storage_write_stats.sql` adds `get_storage_write_stats(...)` so public RPC endpoints can expose bounded storage write analytics (`getstoragewritestats`).
- `026_storage_read_stats.sql` adds `get_storage_read_stats(...)` so public RPC endpoints can expose bounded storage read analytics (`getstoragereadstats`).

Optional automation (runs migrations using a direct Postgres connection string):

```bash
export NEO_STATE_RECORDER__SUPABASE_CONNECTION_STRING='postgresql://...'
dotnet run -c Release --project tools/CreateTables migrate
```

## 3. Create Storage Bucket

1. In Supabase **Storage**, create bucket: `block-state`  
   (or another name, but keep it consistent with env/config).
2. `004_public_read_policies.sql` already includes a public read policy for this bucket.

## 4. Pre‑Create Trace Partitions

Mainnet will grow quickly; pre‑creating partitions avoids runtime DDL.

Run once using the service role (SQL editor is fine):

```sql
-- Create 100k block partitions up to current height + 200k buffer
select ensure_trace_partitions(100000, 200000);
```

You can rerun this periodically (monthly/quarterly) as height grows.

Optional automation (same connection string as above):

```bash
dotnet run -c Release --project tools/CreateTables ensure-trace-partitions 100000 200000
```

## 5. Configure the Indexer Node

1. Copy `.env.example` → `.env` and fill in:

```bash
NEO_STATE_RECORDER__ENABLED=true
NEO_STATE_RECORDER__SUPABASE_URL=https://<project>.supabase.co
NEO_STATE_RECORDER__SUPABASE_KEY=<service_role_key>
NEO_STATE_RECORDER__UPLOAD_MODE=RestApi

# Optional: also upload replayable binary snapshots
# NEO_STATE_RECORDER__UPLOAD_MODE=Both

# Keep JSON/CSV uploads disabled on mainnet unless required
NEO_STATE_RECORDER__UPLOAD_AUX_FORMATS=false

# Throttle concurrent uploads (HTTP and optional direct Postgres)
NEO_STATE_RECORDER__TRACE_UPLOAD_CONCURRENCY=4

# Optional: keep trace/read tables exact on re-sync + tip reorgs (adds DELETE traffic)
# - trims per-transaction stale trace rows when counts shrink
# - deletes per-height rows (reads + traces) when a tip reorg replaces a block hash
# NEO_STATE_RECORDER__TRACE_TRIM_STALE_ROWS=false

# Optional: bounded background upload queues (avoid unbounded memory growth if Supabase is slow/down)
# NEO_STATE_RECORDER__UPLOAD_QUEUE_CAPACITY=2048
# NEO_STATE_RECORDER__TRACE_UPLOAD_QUEUE_CAPACITY=16384
# NEO_STATE_RECORDER__UPLOAD_QUEUE_WORKERS=4

# Optional: cap storage_reads per block (0 = unlimited)
# NEO_STATE_RECORDER__MAX_STORAGE_READS_PER_BLOCK=0
```

2. Ensure your Neo `config.json` uses the recording wrapper:

```json
"Storage": {
  "Engine": "RecordingStore",
  "Path": "Data_LevelDB_{0}"
}
```

3. Verify plugin config (generated under `plugins/BlockStateIndexer/BlockStateIndexer.json`):

```json
{
  "PluginConfiguration": {
    "Enabled": true,
    "Network": 860833102,
    "MinTransactionCount": 1,
    "UploadMode": "RestApi"
  }
}
```

(`UploadMode` here is a filter on top of the env `NEO_STATE_RECORDER__UPLOAD_MODE`.)

Note: `MinTransactionCount` gates only **detailed trace uploads**. The indexer still uploads per‑transaction outcomes (`transaction_results`) and block metadata even when traces are skipped for small blocks.

## 6. Run Mainnet Node + Indexer

From repo root:

```bash
./run-mainnet.sh
```

The script loads `.env`, copies `config.mainnet.json` to `config.json`, and starts `neo-cli`.

Watch logs for:
- successful Supabase REST inserts
- partition selection messages
- any 429/throttling warnings (tune concurrency/batch size if needed)

## 7. Configure and Deploy Frontend

1. Create `frontend/.env`:

```bash
VITE_SUPABASE_URL=https://<project>.supabase.co
VITE_SUPABASE_ANON_KEY=<anon_key>
VITE_SUPABASE_BUCKET=block-state
```

2. Build:

```bash
cd frontend
npm install
npm run build
```

3. Deploy `frontend/dist/` to your static host.

Because RLS allows public SELECT only, the anon key is safe to embed in the frontend.

## 8. Operational Tips

- **Key safety**: never expose the service role key outside the indexer host.
- **Public JSON-RPC**: if you expose trace RPC endpoints to untrusted clients, set:
  - `NEO_RPC_TRACES__SUPABASE_KEY=<anon_key>` and consider lowering `NEO_RPC_TRACES__MAX_CONCURRENCY`.
- **Rate limits**: if you see Supabase 429s, lower:
  - `NEO_STATE_RECORDER__TRACE_UPLOAD_CONCURRENCY`
  - `NEO_STATE_RECORDER__TRACE_BATCH_SIZE`
- **Backpressure**: if Supabase is slow/down and memory grows, tune:
  - `NEO_STATE_RECORDER__UPLOAD_QUEUE_CAPACITY` (reads + block_stats)
  - `NEO_STATE_RECORDER__TRACE_UPLOAD_QUEUE_CAPACITY` (per-tx traces)
  - `NEO_STATE_RECORDER__UPLOAD_QUEUE_WORKERS`
- **Storage read volume**: to protect memory/DB on mainnet, consider setting:
  - `NEO_STATE_RECORDER__MAX_STORAGE_READS_PER_BLOCK` (0 = unlimited)
- **Replay snapshots**:
  - default RestApi mode stores all queryable data.
  - set `UPLOAD_MODE=Both` only if you want `.bin` snapshots for offline replay/export.
- **Pruning / retention**:
  - Trace tables are partitioned; use `prune_trace_partitions(retention_blocks)` to drop old partitions quickly.
  - `storage_reads` is not partitioned; use `prune_storage_reads(retention_blocks)` (migrations `010`, `011`) to delete old rows.
    - For incremental pruning, call `prune_storage_reads(retention_blocks, batch_size, max_batches)` where `max_batches=0` runs until complete.
  - Both are intended for `service_role`/admin use (scheduled jobs). For large deletes, expect table bloat and plan VACUUM during low traffic.
  - Optional automation (same connection string as above):
    - `dotnet run -c Release --project tools/CreateTables prune-trace-partitions <retention_blocks>`
    - `dotnet run -c Release --project tools/CreateTables prune-storage-reads <retention_blocks> [batch_size] [max_batches]`
- **Replay tooling** (optional):
  - Enable the `StateReplay` plugin and set Supabase credentials in `plugins/StateReplay/StateReplay.json`.
  - `replay supabase <blockIndex>` replays using `storage_reads` from Supabase Postgres (no per-block storage files required).
  - `replay download <blockIndex>` downloads `block-<N>.bin` from Supabase Storage (requires binary uploads).
