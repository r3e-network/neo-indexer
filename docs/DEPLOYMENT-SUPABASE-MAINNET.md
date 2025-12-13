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

Notes:
- `002_trace_tables.sql` sets up range partitions and locks down partition management RPCs.
- `003_syscall_names.sql` seeds the `syscall_names` reference table (safe to re-run; uses upsert).
- `004_public_read_policies.sql` enables RLS + public read policies, and restricts writes to service role only.

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

# Throttle concurrent HTTPS writes
NEO_STATE_RECORDER__TRACE_UPLOAD_CONCURRENCY=4
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
- **Rate limits**: if you see Supabase 429s, lower:
  - `NEO_STATE_RECORDER__TRACE_UPLOAD_CONCURRENCY`
  - `NEO_STATE_RECORDER__TRACE_BATCH_SIZE`
- **Replay snapshots**:
  - default RestApi mode stores all queryable data.
  - set `UPLOAD_MODE=Both` only if you want `.bin` snapshots for offline replay/export.
- **Pruning / retention**:
  - Trace tables are partitioned; use `prune_trace_partitions(retention_blocks)` to drop old partitions quickly.
  - `storage_reads` is not partitioned; use `prune_storage_reads(retention_blocks)` (migration `010_prune_storage_reads.sql`) to delete old rows.
  - Both are intended for `service_role`/admin use (scheduled jobs). For large deletes, expect table bloat and plan VACUUM during low traffic.
- **Replay tooling** (optional):
  - Enable the `StateReplay` plugin and set Supabase credentials in `plugins/StateReplay/StateReplay.json`.
  - `replay supabase <blockIndex>` replays using `storage_reads` from Supabase Postgres (no per-block storage files required).
  - `replay download <blockIndex>` downloads `block-<N>.bin` from Supabase Storage (requires binary uploads).
