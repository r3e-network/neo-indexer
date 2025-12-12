-- Migration: 002_trace_tables.sql
-- Description: Create tables for execution trace data (OpCodes, Syscalls, Contract Calls, etc.)
-- Date: 2025-12-11

-- ============================================
-- OpCode Traces Table (Partitioned)
-- ============================================
CREATE TABLE IF NOT EXISTS opcode_traces (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    trace_order INTEGER NOT NULL,
    contract_hash TEXT NOT NULL,
    instruction_pointer INTEGER NOT NULL,
    opcode SMALLINT NOT NULL,
    opcode_name TEXT NOT NULL,
    operand_base64 TEXT,
    gas_consumed BIGINT NOT NULL,
    stack_depth INTEGER,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, trace_order)
) PARTITION BY RANGE (block_index);

-- Create initial partitions (0-100000, 100000-200000, etc.)
CREATE TABLE IF NOT EXISTS opcode_traces_0_100000
    PARTITION OF opcode_traces
    FOR VALUES FROM (0) TO (100000);

CREATE TABLE IF NOT EXISTS opcode_traces_100000_200000
    PARTITION OF opcode_traces
    FOR VALUES FROM (100000) TO (200000);

CREATE TABLE IF NOT EXISTS opcode_traces_200000_300000
    PARTITION OF opcode_traces
    FOR VALUES FROM (200000) TO (300000);

-- Default partition catches all higher block ranges (required for mainnet)
CREATE TABLE IF NOT EXISTS opcode_traces_default
    PARTITION OF opcode_traces DEFAULT;

-- Indexes for opcode_traces
CREATE INDEX IF NOT EXISTS idx_opcode_traces_tx_hash ON opcode_traces(tx_hash);
CREATE INDEX IF NOT EXISTS idx_opcode_traces_contract ON opcode_traces(contract_hash);
CREATE INDEX IF NOT EXISTS idx_opcode_traces_opcode ON opcode_traces(opcode);

-- ============================================
-- Syscall Traces Table (Partitioned)
-- ============================================
CREATE TABLE IF NOT EXISTS syscall_traces (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    trace_order INTEGER NOT NULL,
    contract_hash TEXT NOT NULL,
    syscall_hash TEXT NOT NULL,
    syscall_name TEXT NOT NULL,
    gas_cost BIGINT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, trace_order)
) PARTITION BY RANGE (block_index);

-- Create initial partitions
CREATE TABLE IF NOT EXISTS syscall_traces_0_100000
    PARTITION OF syscall_traces
    FOR VALUES FROM (0) TO (100000);

CREATE TABLE IF NOT EXISTS syscall_traces_100000_200000
    PARTITION OF syscall_traces
    FOR VALUES FROM (100000) TO (200000);

CREATE TABLE IF NOT EXISTS syscall_traces_200000_300000
    PARTITION OF syscall_traces
    FOR VALUES FROM (200000) TO (300000);

-- Default partition catches all higher block ranges (required for mainnet)
CREATE TABLE IF NOT EXISTS syscall_traces_default
    PARTITION OF syscall_traces DEFAULT;

-- Indexes for syscall_traces
CREATE INDEX IF NOT EXISTS idx_syscall_traces_tx_hash ON syscall_traces(tx_hash);
CREATE INDEX IF NOT EXISTS idx_syscall_traces_contract ON syscall_traces(contract_hash);
CREATE INDEX IF NOT EXISTS idx_syscall_traces_name ON syscall_traces(syscall_name);

-- ============================================
-- Contract Calls Table (Partitioned)
-- ============================================
CREATE TABLE IF NOT EXISTS contract_calls (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    trace_order INTEGER NOT NULL,
    caller_hash TEXT,
    callee_hash TEXT NOT NULL,
    method_name TEXT,
    call_depth INTEGER NOT NULL,
    success BOOLEAN NOT NULL DEFAULT true,
    gas_consumed BIGINT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, trace_order)
) PARTITION BY RANGE (block_index);

-- Create initial partitions
CREATE TABLE IF NOT EXISTS contract_calls_0_100000
    PARTITION OF contract_calls
    FOR VALUES FROM (0) TO (100000);

CREATE TABLE IF NOT EXISTS contract_calls_100000_200000
    PARTITION OF contract_calls
    FOR VALUES FROM (100000) TO (200000);

CREATE TABLE IF NOT EXISTS contract_calls_200000_300000
    PARTITION OF contract_calls
    FOR VALUES FROM (200000) TO (300000);

-- Default partition catches all higher block ranges (required for mainnet)
CREATE TABLE IF NOT EXISTS contract_calls_default
    PARTITION OF contract_calls DEFAULT;

-- Indexes for contract_calls
CREATE INDEX IF NOT EXISTS idx_contract_calls_tx_hash ON contract_calls(tx_hash);
CREATE INDEX IF NOT EXISTS idx_contract_calls_caller ON contract_calls(caller_hash);
CREATE INDEX IF NOT EXISTS idx_contract_calls_callee ON contract_calls(callee_hash);

-- ============================================
-- Storage Writes Table (Partitioned)
-- ============================================
CREATE TABLE IF NOT EXISTS storage_writes (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    write_order INTEGER NOT NULL,
    contract_id INTEGER,
    contract_hash TEXT NOT NULL,
    key_base64 TEXT NOT NULL,
    old_value_base64 TEXT,
    new_value_base64 TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, write_order)
) PARTITION BY RANGE (block_index);

-- Create initial partitions
CREATE TABLE IF NOT EXISTS storage_writes_0_100000
    PARTITION OF storage_writes
    FOR VALUES FROM (0) TO (100000);

CREATE TABLE IF NOT EXISTS storage_writes_100000_200000
    PARTITION OF storage_writes
    FOR VALUES FROM (100000) TO (200000);

CREATE TABLE IF NOT EXISTS storage_writes_200000_300000
    PARTITION OF storage_writes
    FOR VALUES FROM (200000) TO (300000);

-- Default partition catches all higher block ranges (required for mainnet)
CREATE TABLE IF NOT EXISTS storage_writes_default
    PARTITION OF storage_writes DEFAULT;

-- Indexes for storage_writes
CREATE INDEX IF NOT EXISTS idx_storage_writes_tx_hash ON storage_writes(tx_hash);
CREATE INDEX IF NOT EXISTS idx_storage_writes_contract ON storage_writes(contract_id);

-- ============================================
-- Notifications Table (Partitioned)
-- ============================================
CREATE TABLE IF NOT EXISTS notifications (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    notification_order INTEGER NOT NULL,
    contract_hash TEXT NOT NULL,
    event_name TEXT NOT NULL,
    state_json JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, notification_order)
) PARTITION BY RANGE (block_index);

-- Create initial partitions
CREATE TABLE IF NOT EXISTS notifications_0_100000
    PARTITION OF notifications
    FOR VALUES FROM (0) TO (100000);

CREATE TABLE IF NOT EXISTS notifications_100000_200000
    PARTITION OF notifications
    FOR VALUES FROM (100000) TO (200000);

CREATE TABLE IF NOT EXISTS notifications_200000_300000
    PARTITION OF notifications
    FOR VALUES FROM (200000) TO (300000);

-- Default partition catches all higher block ranges (required for mainnet)
CREATE TABLE IF NOT EXISTS notifications_default
    PARTITION OF notifications DEFAULT;

-- Indexes for notifications
CREATE INDEX IF NOT EXISTS idx_notifications_tx_hash ON notifications(tx_hash);
CREATE INDEX IF NOT EXISTS idx_notifications_contract ON notifications(contract_hash);
CREATE INDEX IF NOT EXISTS idx_notifications_event ON notifications(event_name);

-- ============================================
-- Block Statistics Table (Not Partitioned)
-- ============================================
CREATE TABLE IF NOT EXISTS block_stats (
    block_index INTEGER PRIMARY KEY,
    tx_count INTEGER NOT NULL DEFAULT 0,
    total_gas_consumed BIGINT NOT NULL DEFAULT 0,
    opcode_count INTEGER NOT NULL DEFAULT 0,
    syscall_count INTEGER NOT NULL DEFAULT 0,
    contract_call_count INTEGER NOT NULL DEFAULT 0,
    storage_read_count INTEGER NOT NULL DEFAULT 0,
    storage_write_count INTEGER NOT NULL DEFAULT 0,
    notification_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Index for block_stats
CREATE INDEX IF NOT EXISTS idx_block_stats_created ON block_stats(created_at);

-- ============================================
-- Syscall Names Reference Table
-- ============================================
CREATE TABLE IF NOT EXISTS syscall_names (
    hash TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    category TEXT,
    description TEXT,
    gas_base BIGINT
);

-- Index for syscall_names
CREATE INDEX IF NOT EXISTS idx_syscall_names_category ON syscall_names(category);
CREATE INDEX IF NOT EXISTS idx_syscall_names_name ON syscall_names(name);

-- ============================================
-- Enable Row Level Security (RLS)
-- ============================================
ALTER TABLE opcode_traces ENABLE ROW LEVEL SECURITY;
ALTER TABLE syscall_traces ENABLE ROW LEVEL SECURITY;
ALTER TABLE contract_calls ENABLE ROW LEVEL SECURITY;
ALTER TABLE storage_writes ENABLE ROW LEVEL SECURITY;
ALTER TABLE notifications ENABLE ROW LEVEL SECURITY;
ALTER TABLE block_stats ENABLE ROW LEVEL SECURITY;
ALTER TABLE syscall_names ENABLE ROW LEVEL SECURITY;

-- Enable RLS on any existing partitions so policies apply consistently.
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
        WHERE parent.relname IN ('opcode_traces','syscall_traces','contract_calls','storage_writes','notifications')
    LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', part.partition_name);
    END LOOP;
END;
$rls_partitions$;

-- Create policies for public read access (idempotent)
DO $policy$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'opcode_traces' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON opcode_traces FOR SELECT USING (true)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'syscall_traces' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON syscall_traces FOR SELECT USING (true)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'contract_calls' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON contract_calls FOR SELECT USING (true)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'storage_writes' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON storage_writes FOR SELECT USING (true)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'notifications' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON notifications FOR SELECT USING (true)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'block_stats' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON block_stats FOR SELECT USING (true)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'syscall_names' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON syscall_names FOR SELECT USING (true)';
    END IF;
END;
$policy$;

-- Create policies for service role insert/update (idempotent).
-- Only the Supabase service key should be able to write trace data.
DO $policy$
BEGIN
    -- Drop any legacy wide-open policies.
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON opcode_traces';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON syscall_traces';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON contract_calls';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON storage_writes';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON notifications';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON block_stats';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON syscall_names';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON opcode_traces';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON syscall_traces';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON contract_calls';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON storage_writes';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON notifications';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON block_stats';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON syscall_names';

    -- Drop prior service-role policies if this migration is re-run.
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON opcode_traces';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON syscall_traces';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON contract_calls';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON storage_writes';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON notifications';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON block_stats';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON syscall_names';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON opcode_traces';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON syscall_traces';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON contract_calls';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON storage_writes';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON notifications';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON block_stats';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON syscall_names';

    -- Only allow writes when JWT role is service_role.
    EXECUTE 'CREATE POLICY "Allow service role insert" ON opcode_traces FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role insert" ON syscall_traces FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role insert" ON contract_calls FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role insert" ON storage_writes FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role insert" ON notifications FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role insert" ON block_stats FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role insert" ON syscall_names FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';

    EXECUTE 'CREATE POLICY "Allow service role upsert" ON opcode_traces FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON syscall_traces FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON contract_calls FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON storage_writes FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON notifications FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON block_stats FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON syscall_names FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
END;
$policy$;

-- ============================================
-- Partition Management Functions
-- ============================================

-- Function to create a new partition for a table
CREATE OR REPLACE FUNCTION create_trace_partition(
    table_name TEXT,
    start_block INTEGER,
    end_block INTEGER
) RETURNS void AS $$
DECLARE
    partition_name TEXT;
BEGIN
    partition_name := table_name || '_' || start_block || '_' || end_block;

    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF %I FOR VALUES FROM (%s) TO (%s)',
        partition_name,
        table_name,
        start_block,
        end_block
    );

    -- Ensure RLS is enabled on the partition so public read policies apply even
    -- if the partition is queried directly.
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', partition_name);

    -- Ensure per-partition indexes exist for fast queries.
    -- New partitions created after parent indexes are defined do NOT automatically get indexes.
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
    END IF;

    RAISE NOTICE 'Created partition: %', partition_name;
END;
$$ LANGUAGE plpgsql;

-- Automatically ensure partitions exist up to current height + lookahead.
-- Call periodically (e.g., via Supabase scheduled function) to keep default partitions empty.
CREATE OR REPLACE FUNCTION ensure_trace_partitions(
    partition_size INTEGER DEFAULT 100000,
    lookahead_blocks INTEGER DEFAULT 100000
) RETURNS void AS $$
DECLARE
    current_height INTEGER;
    target_height INTEGER;
    start_block INTEGER;
    end_block INTEGER;
    table_name TEXT;
    tables TEXT[] := ARRAY['opcode_traces','syscall_traces','contract_calls','storage_writes','notifications'];
BEGIN
    SELECT COALESCE(MAX(block_index), 0) INTO current_height FROM blocks;
    target_height := current_height + lookahead_blocks;

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
$$ LANGUAGE plpgsql;

-- Function to drop old partitions (for pruning)
CREATE OR REPLACE FUNCTION prune_old_partitions(
    table_name TEXT,
    retention_blocks INTEGER
) RETURNS INTEGER AS $$
DECLARE
    current_height INTEGER;
    cutoff_block INTEGER;
    partition_record RECORD;
    dropped_count INTEGER := 0;
    partition_end INTEGER;
BEGIN
    -- Get current max block
    SELECT COALESCE(MAX(block_index), 0) INTO current_height FROM blocks;
    cutoff_block := current_height - retention_blocks;

    IF cutoff_block <= 0 THEN
        RETURN 0;
    END IF;

    -- Find and drop partitions below cutoff
    FOR partition_record IN
        SELECT tablename FROM pg_tables
        WHERE tablename LIKE table_name || '_%'
        AND schemaname = 'public'
    LOOP
        -- Extract end block from partition name (e.g., opcode_traces_0_100000 -> 100000)
        BEGIN
            partition_end := split_part(partition_record.tablename, '_', array_length(string_to_array(partition_record.tablename, '_'), 1))::INTEGER;

            IF partition_end <= cutoff_block THEN
                EXECUTE format('DROP TABLE IF EXISTS %I', partition_record.tablename);
                dropped_count := dropped_count + 1;
                RAISE NOTICE 'Dropped partition: %', partition_record.tablename;
            END IF;
        EXCEPTION WHEN OTHERS THEN
            -- Skip if partition name doesn't match expected format
            CONTINUE;
        END;
    END LOOP;

    RETURN dropped_count;
END;
$$ LANGUAGE plpgsql;

-- Convenience wrapper to prune all trace tables in one call.
-- Returns how many partitions were dropped per table.
CREATE OR REPLACE FUNCTION prune_trace_partitions(
    retention_blocks INTEGER
) RETURNS TABLE (
    table_name TEXT,
    dropped_partitions INTEGER
) AS $$
DECLARE
    t TEXT;
    tables TEXT[] := ARRAY['opcode_traces','syscall_traces','contract_calls','storage_writes','notifications'];
BEGIN
    FOREACH t IN ARRAY tables LOOP
        table_name := t;
        dropped_partitions := prune_old_partitions(t, retention_blocks);
        RETURN NEXT;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- Function to get partition statistics
CREATE OR REPLACE FUNCTION get_partition_stats(table_name TEXT)
RETURNS TABLE (
    partition_name TEXT,
    row_count BIGINT,
    size_bytes BIGINT
) AS $$
BEGIN
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
$$ LANGUAGE plpgsql;

-- ============================================
-- Function Privileges (Supabase)
-- ============================================
-- Partition management functions are admin-only. By default Postgres grants
-- EXECUTE to PUBLIC, which would expose these as PostgREST RPC endpoints.
-- Revoke public access and grant only to service roles.

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

-- ============================================
-- Comments
-- ============================================
COMMENT ON TABLE opcode_traces IS 'Stores OpCode execution traces for each transaction';
COMMENT ON TABLE syscall_traces IS 'Stores syscall invocation traces for each transaction';
COMMENT ON TABLE contract_calls IS 'Stores contract-to-contract call traces';
COMMENT ON TABLE storage_writes IS 'Stores storage write operations with before/after values';
COMMENT ON TABLE notifications IS 'Stores notification events emitted by contracts';
COMMENT ON TABLE block_stats IS 'Aggregated statistics per block';
COMMENT ON TABLE syscall_names IS 'Reference table mapping syscall hashes to human-readable names';
