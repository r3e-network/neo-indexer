# neo_log_pusher

Async CSV shipper for DeepLogger outputs. Watches a directory for stable CSVs and streams them into Postgres/Supabase via COPY.

## Setup
```bash
cp .env.example .env
# set DATABASE_URL to Supabase service role / postgres credentials
cargo run --release
```

## Env
- `DATABASE_URL` (required): Postgres connection string.
- `LOG_DIR` (optional): defaults to `/neo-data/logs`.
- `HTTP_PORT` (optional): default 8080 for health endpoint.

## Behavior
- Scans `LOG_DIR` every 5s.
- A file is considered stable if its mtime is older than 10s.
- Recognized prefixes:
  - `trace_*.csv` -> `op_traces(tx_hash,block_index,step_order,contract_hash,opcode,syscall,gas_consumed,stack_top)`
  - `blocks_*.csv` -> `blocks(index,hash,timestamp,tx_count)`
  - `txs_*.csv` -> `transactions(hash,block_index,sender,sys_fee,net_fee)`
- After successful COPY, the CSV is removed.

## Notes
- Uses `tokio-postgres` COPY streaming with backpressure awareness.
- Keep partitions pre-created in Postgres for incoming `op_traces`.

## Docker
```bash
docker build -t neo-log-pusher .
DATABASE_URL=postgres://... docker run --rm -v $(pwd)/logs:/neo-data/logs neo-log-pusher
```

## Health
- Logs DB connectivity errors continuously; `health::check_health` runs every loop iteration (`SELECT 1`).
- HTTP endpoints on `0.0.0.0:HTTP_PORT`:
  - `/` JSON `{status, db_ok, processed_files, last_success, last_scan, last_error}`
  - `/metrics` Prometheus text with `neo_log_pusher_db_ok`, `neo_log_pusher_processed_files`, timestamps, and last error label.
