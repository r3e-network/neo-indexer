-- Migration: 016_partition_management_transaction_results_support.sql
-- Description: Allow partition stats/pruning helpers to operate on transaction_results partitions.
-- Date: 2025-12-15
--
-- 008 added transaction_results to partition creation/ensure helpers. This migration
-- extends the admin pruning/stats helpers to also accept transaction_results.

-- ============================================
-- prune_old_partitions (extend allowed tables)
-- ============================================

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
    allowed_tables TEXT[] := ARRAY['opcode_traces','syscall_traces','contract_calls','storage_writes','notifications','transaction_results'];
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

-- ============================================
-- get_partition_stats (extend allowed tables)
-- ============================================

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
    allowed_tables TEXT[] := ARRAY['opcode_traces','syscall_traces','contract_calls','storage_writes','notifications','transaction_results'];
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

-- ============================================
-- Function Privileges (idempotent)
-- ============================================

REVOKE ALL ON FUNCTION prune_old_partitions(TEXT, INTEGER) FROM PUBLIC;
REVOKE ALL ON FUNCTION get_partition_stats(TEXT) FROM PUBLIC;

GRANT EXECUTE ON FUNCTION prune_old_partitions(TEXT, INTEGER) TO service_role, postgres;
GRANT EXECUTE ON FUNCTION get_partition_stats(TEXT) TO service_role, postgres;

