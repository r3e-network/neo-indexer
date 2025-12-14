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
-- Partition Management Functions (extend allow-list)
-- ============================================

CREATE OR REPLACE FUNCTION create_trace_partition(
    table_name TEXT,
    start_block INTEGER,
    end_block INTEGER
) RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    partition_name TEXT;
    allowed_tables TEXT[] := ARRAY['opcode_traces','syscall_traces','contract_calls','storage_writes','notifications','runtime_logs','transaction_results'];
BEGIN
    IF table_name IS NULL OR NOT (table_name = ANY(allowed_tables)) THEN
        RAISE EXCEPTION 'invalid trace table name: %', table_name
            USING ERRCODE = '22023';
    END IF;
    IF start_block IS NULL OR end_block IS NULL OR start_block < 0 OR end_block <= start_block THEN
        RAISE EXCEPTION 'invalid partition range (% - %)', start_block, end_block
            USING ERRCODE = '22023';
    END IF;

    partition_name := table_name || '_' || start_block || '_' || end_block;

    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF %I FOR VALUES FROM (%s) TO (%s)',
        partition_name,
        table_name,
        start_block,
        end_block
    );

    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', partition_name);

    IF table_name = 'opcode_traces' THEN
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (tx_hash)', partition_name || '_tx_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (contract_hash)', partition_name || '_contract_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (opcode)', partition_name || '_opcode_idx', partition_name);
    ELSIF table_name = 'syscall_traces' THEN
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (tx_hash)', partition_name || '_tx_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (contract_hash)', partition_name || '_contract_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (syscall_name)', partition_name || '_syscall_name_idx', partition_name);
    ELSIF table_name = 'contract_calls' THEN
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (tx_hash)', partition_name || '_tx_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (caller_hash)', partition_name || '_caller_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (callee_hash)', partition_name || '_callee_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (method_name)', partition_name || '_method_name_idx', partition_name);
    ELSIF table_name = 'storage_writes' THEN
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (tx_hash)', partition_name || '_tx_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (contract_id)', partition_name || '_contract_id_idx', partition_name);
    ELSIF table_name = 'notifications' THEN
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (tx_hash)', partition_name || '_tx_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (contract_hash)', partition_name || '_contract_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (event_name)', partition_name || '_event_name_idx', partition_name);
    ELSIF table_name = 'runtime_logs' THEN
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (tx_hash)', partition_name || '_tx_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (contract_hash)', partition_name || '_contract_hash_idx', partition_name);
    ELSIF table_name = 'transaction_results' THEN
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (tx_hash)', partition_name || '_tx_hash_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (success)', partition_name || '_success_idx', partition_name);
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I (vm_state)', partition_name || '_vm_state_idx', partition_name);
    END IF;

    RAISE NOTICE 'Created partition: %', partition_name;
END;
$$;

CREATE OR REPLACE FUNCTION ensure_trace_partitions(
    partition_size INTEGER DEFAULT 100000,
    lookahead_blocks INTEGER DEFAULT 100000
) RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    current_height INTEGER;
    target_height INTEGER;
    start_block INTEGER;
    end_block INTEGER;
    table_name TEXT;
    tables TEXT[] := ARRAY['opcode_traces','syscall_traces','contract_calls','storage_writes','notifications','runtime_logs','transaction_results'];
BEGIN
    IF partition_size IS NULL OR partition_size <= 0 THEN
        RAISE EXCEPTION 'partition_size must be positive'
            USING ERRCODE = '22023';
    END IF;

    SELECT COALESCE(MAX(block_index), 0) INTO current_height FROM blocks;
    target_height := current_height + COALESCE(lookahead_blocks, 0);

    start_block := (current_height / partition_size) * partition_size;
    IF start_block < 0 THEN
        start_block := 0;
    END IF;

    WHILE start_block <= target_height LOOP
        end_block := start_block + partition_size;
        FOREACH table_name IN ARRAY tables LOOP
            PERFORM create_trace_partition(table_name, start_block, end_block);
        END LOOP;
        start_block := end_block;
    END LOOP;
END;
$$;

CREATE OR REPLACE FUNCTION prune_old_partitions(
    table_name TEXT,
    retention_blocks INTEGER
) RETURNS INTEGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    current_height INTEGER;
    cutoff_block INTEGER;
    partition_record RECORD;
    dropped_count INTEGER := 0;
    partition_end INTEGER;
    allowed_tables TEXT[] := ARRAY['opcode_traces','syscall_traces','contract_calls','storage_writes','notifications','runtime_logs','transaction_results'];
BEGIN
    IF table_name IS NULL OR NOT (table_name = ANY(allowed_tables)) THEN
        RAISE EXCEPTION 'invalid trace table name: %', table_name
            USING ERRCODE = '22023';
    END IF;

    SELECT COALESCE(MAX(block_index), 0) INTO current_height FROM blocks;
    cutoff_block := current_height - retention_blocks;

    IF cutoff_block <= 0 THEN
        RETURN 0;
    END IF;

    FOR partition_record IN
        SELECT tablename FROM pg_tables
        WHERE tablename LIKE table_name || '_%'
          AND schemaname = 'public'
    LOOP
        BEGIN
            partition_end := split_part(partition_record.tablename, '_',
                array_length(string_to_array(partition_record.tablename, '_'), 1))::INTEGER;

            IF partition_end <= cutoff_block THEN
                EXECUTE format('DROP TABLE IF EXISTS %I', partition_record.tablename);
                dropped_count := dropped_count + 1;
                RAISE NOTICE 'Dropped partition: %', partition_record.tablename;
            END IF;
        EXCEPTION WHEN OTHERS THEN
            CONTINUE;
        END;
    END LOOP;

    RETURN dropped_count;
END;
$$;

CREATE OR REPLACE FUNCTION prune_trace_partitions(
    retention_blocks INTEGER
) RETURNS TABLE (
    table_name TEXT,
    dropped_partitions INTEGER
)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    t TEXT;
    tables TEXT[] := ARRAY['opcode_traces','syscall_traces','contract_calls','storage_writes','notifications','runtime_logs','transaction_results'];
BEGIN
    FOREACH t IN ARRAY tables LOOP
        table_name := t;
        dropped_partitions := prune_old_partitions(t, retention_blocks);
        RETURN NEXT;
    END LOOP;
END;
$$;

CREATE OR REPLACE FUNCTION get_partition_stats(table_name TEXT)
RETURNS TABLE (
    partition_name TEXT,
    row_count BIGINT,
    size_bytes BIGINT
)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    allowed_tables TEXT[] := ARRAY['opcode_traces','syscall_traces','contract_calls','storage_writes','notifications','runtime_logs','transaction_results'];
BEGIN
    IF table_name IS NULL OR NOT (table_name = ANY(allowed_tables)) THEN
        RAISE EXCEPTION 'invalid trace table name: %', table_name
            USING ERRCODE = '22023';
    END IF;

    RETURN QUERY
    SELECT
        t.tablename::TEXT,
        (SELECT reltuples::BIGINT FROM pg_class WHERE relname = t.tablename),
        pg_relation_size(t.tablename::regclass)
    FROM pg_tables t
    WHERE t.tablename LIKE table_name || '_%'
      AND t.schemaname = 'public'
    ORDER BY t.tablename;
END;
$$;

REVOKE ALL ON FUNCTION create_trace_partition(TEXT, INTEGER, INTEGER) FROM PUBLIC;
REVOKE ALL ON FUNCTION ensure_trace_partitions(INTEGER, INTEGER) FROM PUBLIC;
REVOKE ALL ON FUNCTION prune_old_partitions(TEXT, INTEGER) FROM PUBLIC;
REVOKE ALL ON FUNCTION prune_trace_partitions(INTEGER) FROM PUBLIC;
REVOKE ALL ON FUNCTION get_partition_stats(TEXT) FROM PUBLIC;

GRANT EXECUTE ON FUNCTION create_trace_partition(TEXT, INTEGER, INTEGER) TO service_role, postgres;
GRANT EXECUTE ON FUNCTION ensure_trace_partitions(INTEGER, INTEGER) TO service_role, postgres;
GRANT EXECUTE ON FUNCTION prune_old_partitions(TEXT, INTEGER) TO service_role, postgres;
GRANT EXECUTE ON FUNCTION prune_trace_partitions(INTEGER) TO service_role, postgres;
GRANT EXECUTE ON FUNCTION get_partition_stats(TEXT) TO service_role, postgres;

