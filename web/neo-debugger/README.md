# Neo Debugger UI

Next.js 14 + Tailwind frontend for opcode-level exploration on Neo N3.

## Setup
```bash
npm install
cp .env.local.example .env.local
npm run dev
```

## Environment
- `NEXT_PUBLIC_SUPABASE_URL` – Supabase project URL.
- `NEXT_PUBLIC_SUPABASE_ANON_KEY` – anon/public key (provided).
- `NEXT_PUBLIC_REFRESH_MS` – optional auto-refresh interval for landing widgets (default 15000 ms, clamped 5s-60s).
- `SUPABASE_SERVICE_ROLE_KEY` – optional; if set, server/API routes use this for Supabase reads (kept server-side, not exposed to client).
- `REVALIDATE_TOKEN` – optional; if set, `/api/revalidate` requires `Authorization: Bearer <token>` to trigger ISR revalidation.
- Rate limiting: `/api/search` has a simple in-memory rate limit (30 req/min per IP).
 - Note: lockfile not generated yet (offline). When online, run `npm install` in `web/neo-debugger` (Node 20 per `.nvmrc`) to produce `package-lock.json` for CI/Netlify `npm ci`.

## Pages
- `/` – search landing with live stats (latest block, tx/opcode counts est., ingestion lag, recent blocks, opcode volume, latest opcodes). Auto-refresh with pause/interval control and persisted preferences.
- `/trace/:txid` – opcode trace; filters: `opcode`, `contract`, `syscall`, `page`, `pageSize` (50–1000). Includes quick "Clear" reset.
- `/block/:height` – block overview; filters: `hash`, `sender`, `opcode`, `page`, `pageSize` (50–500). Includes quick "Clear" reset.

## APIs
- `/api/stats` – latest block, recent blocks, counts, ingestion lag.
- `/api/opcode-volume` – opcode counts grouped by block (~last 20 blocks).
- `/api/live-opcodes` – newest opcode rows (block/tx/step/opcode/syscall/contract/gas).
- `/api/health` – health probe that returns latest block index.
- `/api/revalidate` – POST `{ path }` with `Authorization: Bearer <REVALIDATE_TOKEN>` to revalidate ISR paths.
- `/api/search` – block by index and transaction by hash/sender prefix (used by landing typeahead).

## Notes
- Landing search supports typeahead (hash/sender prefixes) and falls back to direct trace/block routing (requires hex-like input for tx fallback).
- Tx hash validation expects `0x` + 64 hex characters; non-matching inputs show a guidance message.
- `/api/search` is rate-limited (30 req/min per IP, in-memory).

## Docker
```bash
docker build -t neo-debugger-ui .
NEXT_PUBLIC_SUPABASE_URL=... NEXT_PUBLIC_SUPABASE_ANON_KEY=... docker run --rm -p 3000:3000 neo-debugger-ui
```

## Netlify
- Root `netlify.toml` points here with `@netlify/plugin-nextjs`.
- Set env vars in Netlify dashboard:
  - `NEXT_PUBLIC_SUPABASE_URL` - Your Supabase project URL
  - `NEXT_PUBLIC_SUPABASE_ANON_KEY` - Your Supabase anon/public key
  - `NEXT_PUBLIC_REFRESH_MS` (optional)
- Build command: `npm run build`; publish directory: `.next`; functions: `.netlify/functions` (auto by plugin).
