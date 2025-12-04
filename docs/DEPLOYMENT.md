# Neo Deep Trace Deployment Guide

## Architecture Overview

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Neo N3 Node   │────▶│  Rust Pusher    │────▶│    Supabase     │
│  + DeepLogger   │ CSV │  (neo_log_pusher)│COPY │   PostgreSQL    │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                                                        │
                                                        ▼
                                               ┌─────────────────┐
                                               │   Next.js Web   │
                                               │  (neo-debugger) │
                                               └─────────────────┘
```

## Prerequisites

- Docker & Docker Compose
- Neo N3 node (neo-cli 3.6+)
- Supabase account (or self-hosted PostgreSQL 15+)
- Node.js 20+ (for local development)
- Rust 1.78+ (for local development)
- .NET 8.0 SDK (for plugin compilation)

## Quick Start (Docker Compose)

### 1. Clone and Configure

```bash
git clone https://github.com/your-org/neo-indexer.git
cd neo-indexer

# Copy environment template
cp .env.example .env
```

### 2. Edit `.env`

```env
# Supabase/PostgreSQL
DATABASE_URL=postgresql://postgres:password@db:5432/postgres

# For local Docker Compose (uses local postgres)
# DATABASE_URL=postgresql://postgres:postgres@db:5432/postgres

# Web Frontend
NEXT_PUBLIC_SUPABASE_URL=https://your-project.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=your-anon-key
SUPABASE_SERVICE_ROLE_KEY=your-service-role-key

# Optional
NEXT_PUBLIC_REFRESH_MS=15000
```

### 3. Initialize Database

```bash
# Option A: Using Supabase Dashboard
# Run supabase/schema.sql in SQL Editor
# Then run supabase/policies.sql

# Option B: Using Docker Compose (local postgres)
docker compose up db db-init -d
```

### 4. Start Services

```bash
# Start all services
docker compose up -d

# View logs
docker compose logs -f pusher
docker compose logs -f web
```

### 5. Deploy Neo Plugin

```bash
# Build the plugin
cd csharp/Neo.Plugins.DeepLogger
dotnet build -c Release

# Copy to neo-cli plugins directory
cp bin/Release/net8.0/Neo.Plugins.DeepLogger.dll /path/to/neo-cli/Plugins/

# Configure environment (in neo-cli directory)
export DEEPLOGGER_LOG_DIR=/neo-data/logs
export DEEPLOGGER_ROTATE_BLOCKS=1000

# Start neo-cli
./neo-cli
```

## Production Deployment

### Supabase Setup

1. Create a new Supabase project
2. Run `supabase/schema.sql` in SQL Editor
3. Run `supabase/policies.sql` for RLS
4. Copy project URL and keys to `.env`

### Netlify Deployment (Web)

1. Connect repository to Netlify
2. Set build settings:
   - Base directory: `web/neo-debugger`
   - Build command: `npm run build:netlify`
   - Publish directory: `.next`
3. Add environment variables in Netlify dashboard
4. Deploy

### Rust Pusher (VPS/Cloud)

```bash
# Build release binary
cd rust/neo_log_pusher
cargo build --release

# Or use Docker
docker build -t neo-log-pusher .
docker run -d \
  -e DATABASE_URL="postgresql://..." \
  -e LOG_DIR=/neo-data/logs \
  -e HTTP_PORT=8080 \
  -v /neo-data/logs:/neo-data/logs \
  -p 8080:8080 \
  neo-log-pusher
```

### Neo Node Setup

```bash
# Ensure shared volume for CSV files
mkdir -p /neo-data/logs

# Start neo-cli with plugin
cd /path/to/neo-cli
export DEEPLOGGER_LOG_DIR=/neo-data/logs
./neo-cli
```

## Partition Management

### Create New Partitions

As the blockchain grows, create new partitions:

```sql
-- Create partition for blocks 500000-600000
SELECT create_partition(500000, 600000);

-- Verify partitions
SELECT tablename FROM pg_tables WHERE tablename LIKE 'op_traces_p%';
```

### Drop Old Partitions

To free storage, drop old partitions:

```sql
-- Drop partition for blocks 0-100000 (instant operation)
SELECT drop_partition(0, 100000);
```

## Monitoring

### Rust Pusher Metrics

The pusher exposes Prometheus metrics at `/metrics`:

```bash
curl http://localhost:8080/metrics
```

Metrics:
- `neo_log_pusher_db_ok` - Database connectivity
- `neo_log_pusher_processed_files` - Total files processed
- `neo_log_pusher_last_success_timestamp` - Last successful upload
- `neo_log_pusher_last_scan_timestamp` - Last directory scan

### Health Checks

```bash
# Pusher health
curl http://localhost:8080/

# Web health
curl https://your-deployment.netlify.app/api/health
```

## Troubleshooting

### CSV Files Not Being Processed

1. Check file permissions: `ls -la /neo-data/logs/`
2. Verify pusher can read: `docker compose logs pusher`
3. Check file stability (10s delay before processing)

### Database Connection Issues

1. Verify `DATABASE_URL` format
2. Check network connectivity
3. Verify SSL settings for Supabase

### High Memory Usage (Web)

1. Check rate limiter bucket count
2. Reduce `NEXT_PUBLIC_REFRESH_MS`
3. Scale horizontally with multiple instances

### Missing Partitions

```sql
-- Check existing partitions
SELECT tablename FROM pg_tables WHERE tablename LIKE 'op_traces_p%';

-- Create missing partition
SELECT create_partition(START_INDEX, END_INDEX);
```

## Scaling Considerations

### Database
- Use connection pooling (PgBouncer)
- Consider read replicas for web queries
- Partition size: 100,000 blocks recommended

### Pusher
- Single instance per node
- Horizontal scaling not needed (file-based)

### Web
- Stateless, scale horizontally
- Use CDN for static assets
- Consider edge functions for API

## Security Checklist

- [ ] RLS enabled on all tables
- [ ] Service role key not exposed to client
- [ ] Rate limiting configured
- [ ] HTTPS enforced
- [ ] Database credentials rotated
- [ ] Firewall rules for pusher
