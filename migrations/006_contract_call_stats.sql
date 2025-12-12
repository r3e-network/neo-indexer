-- Migration: 006_contract_call_stats.sql
-- Description: Supabase RPC function for contract/native method statistics.
-- Date: 2025-12-12
--
-- Provides aggregated contract call usage over a block range. Useful for
-- tracking native contract method hot spots and call graphs from the frontend.

CREATE OR REPLACE FUNCTION get_contract_call_stats(
    start_block INTEGER,
    end_block INTEGER,
    p_callee_hash TEXT DEFAULT NULL,
    p_caller_hash TEXT DEFAULT NULL,
    p_method_name TEXT DEFAULT NULL,
    limit_rows INTEGER DEFAULT 100,
    offset_rows INTEGER DEFAULT 0
)
RETURNS TABLE (
    callee_hash TEXT,
    caller_hash TEXT,
    method_name TEXT,
    call_count BIGINT,
    success_count BIGINT,
    failure_count BIGINT,
    total_gas_consumed BIGINT,
    avg_gas_consumed DOUBLE PRECISION,
    first_block INTEGER,
    last_block INTEGER,
    total_rows BIGINT
)
LANGUAGE sql
STABLE
AS $$
    WITH aggregated AS (
        SELECT
            t.callee_hash,
            t.caller_hash,
            t.method_name,
            COUNT(*) AS call_count,
            SUM(CASE WHEN t.success THEN 1 ELSE 0 END) AS success_count,
            SUM(CASE WHEN NOT t.success THEN 1 ELSE 0 END) AS failure_count,
            COALESCE(SUM(t.gas_consumed), 0) AS total_gas_consumed,
            AVG(t.gas_consumed)::DOUBLE PRECISION AS avg_gas_consumed,
            MIN(t.block_index) AS first_block,
            MAX(t.block_index) AS last_block
        FROM contract_calls t
        WHERE t.block_index >= start_block
          AND t.block_index <= end_block
          AND (p_callee_hash IS NULL OR t.callee_hash = p_callee_hash)
          AND (p_caller_hash IS NULL OR t.caller_hash = p_caller_hash)
          AND (p_method_name IS NULL OR t.method_name = p_method_name)
        GROUP BY t.callee_hash, t.caller_hash, t.method_name
    )
    SELECT aggregated.*, COUNT(*) OVER() AS total_rows
    FROM aggregated
    ORDER BY call_count DESC
    LIMIT limit_rows
    OFFSET offset_rows;
$$;

REVOKE ALL ON FUNCTION get_contract_call_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION get_contract_call_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) TO anon, authenticated;

