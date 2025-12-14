-- Migration: 014_transaction_results.sql
-- Description: Store per-transaction execution results for SQL analytics.
-- Date: 2025-12-14
--
-- This table complements the per-opcode/syscall traces by capturing the final
-- outcome of each transaction execution (VMState, gas consumed, fault info, and
-- result stack).

-- ============================================
-- Transaction Results Table (Partitioned)
-- ============================================

CREATE TABLE IF NOT EXISTS transaction_results (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    vm_state SMALLINT NOT NULL,
    vm_state_name TEXT NOT NULL,
    success BOOLEAN NOT NULL,
    gas_consumed BIGINT NOT NULL,
    fault_exception TEXT,
    result_stack_json JSONB,
    opcode_count INTEGER NOT NULL DEFAULT 0,
    syscall_count INTEGER NOT NULL DEFAULT 0,
    contract_call_count INTEGER NOT NULL DEFAULT 0,
    storage_write_count INTEGER NOT NULL DEFAULT 0,
    notification_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash)
) PARTITION BY RANGE (block_index);

-- Create initial partitions (0-100000, 100000-200000, etc.)
CREATE TABLE IF NOT EXISTS transaction_results_0_100000
    PARTITION OF transaction_results
    FOR VALUES FROM (0) TO (100000);

CREATE TABLE IF NOT EXISTS transaction_results_100000_200000
    PARTITION OF transaction_results
    FOR VALUES FROM (100000) TO (200000);

CREATE TABLE IF NOT EXISTS transaction_results_200000_300000
    PARTITION OF transaction_results
    FOR VALUES FROM (200000) TO (300000);

-- Default partition catches all higher block ranges (required for mainnet)
CREATE TABLE IF NOT EXISTS transaction_results_default
    PARTITION OF transaction_results DEFAULT;

-- Indexes for transaction_results
CREATE INDEX IF NOT EXISTS idx_transaction_results_tx_hash ON transaction_results(tx_hash);
CREATE INDEX IF NOT EXISTS idx_transaction_results_success ON transaction_results(success);
CREATE INDEX IF NOT EXISTS idx_transaction_results_vm_state ON transaction_results(vm_state);

-- ============================================
-- Enable Row Level Security (RLS)
-- ============================================

ALTER TABLE transaction_results ENABLE ROW LEVEL SECURITY;

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
        WHERE parent.relname IN ('transaction_results')
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
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'transaction_results' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON transaction_results FOR SELECT USING (true)';
    END IF;
END;
$policy$;

-- Service role write policies (idempotent). The indexer uses the Supabase service role key.
DO $policy$
BEGIN
    -- Drop any legacy wide-open policies.
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON transaction_results';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON transaction_results';

    -- Drop prior service-role policies if this migration is re-run.
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON transaction_results';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON transaction_results';

    -- Only allow writes when JWT role is service_role.
    EXECUTE 'CREATE POLICY "Allow service role insert" ON transaction_results FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON transaction_results FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
END;
$policy$;

