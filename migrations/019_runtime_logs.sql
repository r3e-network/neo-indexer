-- Migration: 019_runtime_logs.sql
-- Description: Persist System.Runtime.Log events as a partitioned trace table.
-- Date: 2025-12-15
--
-- Runtime logs are distinct from notifications (System.Runtime.Notify) and are
-- useful for SQL analytics, debugging, and forensics.

-- ============================================
-- Runtime Logs Table (Partitioned)
-- ============================================

CREATE TABLE IF NOT EXISTS runtime_logs (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    log_order INTEGER NOT NULL,
    contract_hash TEXT NOT NULL,
    message TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, log_order)
) PARTITION BY RANGE (block_index);

-- Create initial partitions
CREATE TABLE IF NOT EXISTS runtime_logs_0_100000
    PARTITION OF runtime_logs
    FOR VALUES FROM (0) TO (100000);

CREATE TABLE IF NOT EXISTS runtime_logs_100000_200000
    PARTITION OF runtime_logs
    FOR VALUES FROM (100000) TO (200000);

CREATE TABLE IF NOT EXISTS runtime_logs_200000_300000
    PARTITION OF runtime_logs
    FOR VALUES FROM (200000) TO (300000);

-- Default partition catches all higher block ranges (required for mainnet)
CREATE TABLE IF NOT EXISTS runtime_logs_default
    PARTITION OF runtime_logs DEFAULT;

-- Indexes for runtime_logs
CREATE INDEX IF NOT EXISTS idx_runtime_logs_tx_hash ON runtime_logs(tx_hash);
CREATE INDEX IF NOT EXISTS idx_runtime_logs_contract ON runtime_logs(contract_hash);

-- ============================================
-- Enable Row Level Security (RLS)
-- ============================================

ALTER TABLE runtime_logs ENABLE ROW LEVEL SECURITY;

-- Partitions do not automatically inherit the parent's RLS flag.
DO $rls_partitions$
DECLARE
    part RECORD;
BEGIN
    FOR part IN
        SELECT child.relname AS partition_name
        FROM pg_class parent
        JOIN pg_inherits i ON i.inhparent = parent.oid
        JOIN pg_class child ON child.oid = i.inhrelid
        WHERE parent.relname IN ('runtime_logs')
    LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', part.partition_name);
    END LOOP;
END;
$rls_partitions$;

-- ============================================
-- Policies
-- ============================================

-- Public read policy (idempotent)
DO $policy$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'runtime_logs' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON runtime_logs FOR SELECT USING (true)';
    END IF;
END;
$policy$;

-- Service role write policies (idempotent). The indexer uses the Supabase service role key.
DO $policy$
BEGIN
    -- Drop any legacy wide-open policies.
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON runtime_logs';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON runtime_logs';

    -- Drop prior service-role policies if this migration is re-run.
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON runtime_logs';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON runtime_logs';

    -- Only allow writes when JWT role is service_role.
    EXECUTE 'CREATE POLICY "Allow service role insert" ON runtime_logs FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON runtime_logs FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
END;
$policy$;

-- DELETE policy for service role (used by pruning/reorg cleanup).
DO $policy$
BEGIN
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role delete" ON runtime_logs';
    EXECUTE 'CREATE POLICY "Allow service role delete" ON runtime_logs FOR DELETE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
END;
$policy$;

COMMENT ON POLICY "Allow service role delete" ON runtime_logs IS 'Allow service role to delete trace data for pruning or corrections';

-- ============================================
-- Partition Management
-- ============================================

-- Partition management functions are defined in earlier migrations and are
-- extended to include `runtime_logs` by:
-- - migrations/029_partition_management_runtime_logs.sql
