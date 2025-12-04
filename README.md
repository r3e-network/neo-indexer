# Neo Indexer Turnkey Stack

Full-stack solution to capture every Neo N3 mainnet opcode, push it into a partitioned Postgres (Supabase) store, and expose a debugger-style UI.

[![CI](https://github.com/your-org/neo-indexer/actions/workflows/ci.yml/badge.svg)](https://github.com/your-org/neo-indexer/actions/workflows/ci.yml)

## Features

- **Complete OpCode Tracing** - Capture every VM instruction execution
- **Syscall Name Resolution** - Human-readable syscall names (System.Storage.Get, etc.)
- **Contract Call Graph** - Visual representation of contract interactions
- **Partitioned Storage** - Instant data pruning with PostgreSQL partitions
- **Real-time Dashboard** - Live stats, recent blocks, opcode volume charts
- **Advanced Filtering** - Filter traces by opcode, contract, syscall

## Documentation

- [API Reference](docs/API.md) - REST API endpoints and schemas
- [Deployment Guide](docs/DEPLOYMENT.md) - Production deployment instructions

## Repo Layout
- `supabase/schema.sql` – partitioned tables + helpers for fast drop/create.
- `csharp/Neo.Plugins.DeepLogger/` – `neo-cli` persistence plugin that replays transactions and streams CSV logs.
- `rust/neo_log_pusher/` – async log shipper using Postgres COPY with retry logic.
- `web/neo-debugger/` – Next.js 14 + Tailwind UI for search/trace browsing.
- `docs/` – API documentation and deployment guides.

## Quick Start
1. **Database (Supabase)**
   - Open Supabase SQL Editor and run `supabase/schema.sql`.
   - Adjust `create_partition` calls as your height grows; drop with `drop_partition` for instant retention pruning.
2. **DeepLogger (C#)**
   - `cd csharp/Neo.Plugins.DeepLogger`
   - Restore packages (`dotnet restore`) and copy the built DLLs into `neo-cli/Plugins`.
   - Start your Neo node; CSVs will land in `/neo-data/logs` (configurable).
3. **Rust Shipper**
   - `cd rust/neo_log_pusher`
   - Create `.env` with `DATABASE_URL` pointing to Supabase Postgres (service-role credentials recommended).
   - `cargo run --release`; watcher uploads any stable CSV to `blocks`, `transactions`, `op_traces`.
4. **Web UI**
   - `cd web/neo-debugger`
   - `npm install`
   - Copy `.env.local.example` to `.env.local` (anon key is public) and start with `npm run dev`.

## Environment Variables

Copy `.env.example` to `.env` and fill in your Supabase credentials:
```
NEXT_PUBLIC_SUPABASE_URL=https://YOUR_PROJECT_REF.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=your-anon-key
DATABASE_URL=postgres://postgres.YOUR_PROJECT_REF:YOUR_PASSWORD@aws-0-us-east-1.pooler.supabase.com:6543/postgres
```

Get these values from your Supabase Dashboard → Settings → API.

## Data Lifecycle
- Tables are range-partitioned by `block_index`; indexes automatically cascade to partitions.
- Use `create_partition(start, end)` to pre-provision; `drop_partition(start, end)` to delete old ranges in seconds.

## Frontend Endpoints
- `/` – search landing with live stats (latest block, tx/opcode estimated counts, ingestion lag, recent blocks).
- `/trace/:txid` – opcode trace with pagination and filters (`page`, `pageSize`, `opcode`, `contract`, `syscall`).
- `/block/:height` – block overview with tx list pagination and filters (`page`, `pageSize`, `hash`, `sender`, `opcode`).
- `/api/stats` – JSON for latest block, recent blocks, counts, and ingestion lag.
- `/api/opcode-volume` – JSON opcode counts grouped by block for the last ~20 blocks.
- `/api/live-opcodes` – latest opcode rows (block/tx/step/opcode/syscall/contract/gas), ordered by newest.
- `/api/health` – basic health probe (latest block index).
- `/api/search` – block lookup by index and transaction lookup by hash/sender prefix (for typeahead).
- Pusher HTTP: `http://pusher:8080/` JSON health/metrics; `/metrics` Prometheus format.
- Rate limiting: `/api/search` is rate-limited (30 req/min per IP) with an in-memory bucket.

### Frontend config
- `NEXT_PUBLIC_REFRESH_MS` (optional): auto-refresh interval for landing page stats/widgets (default 15000 ms).
- `SUPABASE_SERVICE_ROLE_KEY` (optional, server-only): if set, server/API routes will read with service key (kept off the client bundle).

## Netlify Deployment
- Config: `netlify.toml` at repo root points to `web/neo-debugger` (Next.js 14 with Netlify plugin).
- Set environment variables in Netlify UI:
  - `NEXT_PUBLIC_SUPABASE_URL`
  - `NEXT_PUBLIC_SUPABASE_ANON_KEY`
  - Optional: `NEXT_PUBLIC_REFRESH_MS=15000`
  - Optional server-only: `SUPABASE_SERVICE_ROLE_KEY`, `REVALIDATE_TOKEN`
- Build command: `npm run build:netlify`; publish: `.next`; functions: `.netlify/functions` (handled by `@netlify/plugin-nextjs`).
- Context envs in `netlify.toml` are blank placeholders; UI values take precedence.

## Docker Compose
Build and run the shipper + UI (expects external Supabase DB and a log directory):
```bash
cp .env.example .env  # fill DATABASE_URL with service role / postgres credentials
DATABASE_URL=postgres://... \
docker compose up --build
```
- Mounts `./logs` to `/neo-data/logs` for the pusher.
- UI on `http://localhost:3000`.
- Optional local Postgres service is included (postgres:15). To use it, keep `DATABASE_URL=postgres://postgres:postgres@localhost:5432/postgres` and run `psql -h localhost -U postgres -f supabase/schema.sql` to seed tables/partitions.
- `db-init` service auto-applies `supabase/schema.sql` to the local Postgres on startup (uses postgres/postgres creds). For external DBs, skip or remove `db-init`.

## CI
- Workflow: `.github/workflows/ci.yml`
- Jobs: Next.js lint + `build:netlify`, Rust `fmt`/`check`, .NET restore/build.
- When network is available, generate `web/neo-debugger/package-lock.json` with `npm install` to enable `npm ci` in CI.
- Node: project targets Node 20 (`.nvmrc`) for web builds/Netlify/CI.
- Note: lockfile not committed yet (offline install). Run `npm install` in `web/neo-debugger` (Node 20) when online to create `package-lock.json`.

## Supabase RLS (optional)
- After running `supabase/schema.sql`, you can enable read-only policies via `supabase/policies.sql` to allow public reads (anon key) and restrict writes to service role.

## Notes
- CSV rotation happens every 1000 blocks; upload only occurs after files are stable for 10s.
- `stack_top` is truncated to 64 chars and sanitized for CSV safety.
- Frontend trace page currently limits to 2000 steps; add pagination for huge traces.
