-- Migration: 017_prune_trace_partitions_include_transaction_results.sql
-- Description: Include transaction_results in prune_trace_partitions helper.
-- Date: 2025-12-15
--
-- transaction_results is partitioned by block_index and should be pruned alongside
-- other trace tables when using prune_trace_partitions(retention_blocks).

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
    tables TEXT[] := ARRAY['opcode_traces','syscall_traces','contract_calls','storage_writes','notifications','transaction_results'];
BEGIN
    FOREACH t IN ARRAY tables LOOP
        table_name := t;
        dropped_partitions := prune_old_partitions(t, retention_blocks);
        RETURN NEXT;
    END LOOP;
END;
$$;

REVOKE ALL ON FUNCTION prune_trace_partitions(INTEGER) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION prune_trace_partitions(INTEGER) TO service_role, postgres;

