-- Migration: 005_stats_functions.sql
-- Description: Supabase RPC functions for syscall and opcode statistics.
-- Date: 2025-12-12
--
-- These functions provide efficient, parameterized aggregations that the
-- frontend can call directly via supabase-js `rpc`, avoiding large downloads.

-- ============================================
-- Syscall stats RPC
-- ============================================

CREATE OR REPLACE FUNCTION get_syscall_stats(
    start_block INTEGER,
    end_block INTEGER,
    contract_hash TEXT DEFAULT NULL,
    transaction_hash TEXT DEFAULT NULL,
    syscall_name TEXT DEFAULT NULL,
    limit_rows INTEGER DEFAULT 100,
    offset_rows INTEGER DEFAULT 0
)
RETURNS TABLE (
    syscall_hash TEXT,
    syscall_name TEXT,
    category TEXT,
    call_count BIGINT,
    total_gas_cost BIGINT,
    avg_gas_cost DOUBLE PRECISION,
    min_gas_cost BIGINT,
    max_gas_cost BIGINT,
    first_block INTEGER,
    last_block INTEGER,
    gas_base BIGINT,
    total_rows BIGINT
)
LANGUAGE sql
STABLE
AS $$
    WITH aggregated AS (
        SELECT
            t.syscall_hash,
            t.syscall_name,
            n.category,
            COUNT(*) AS call_count,
            COALESCE(SUM(t.gas_cost), 0) AS total_gas_cost,
            AVG(t.gas_cost)::DOUBLE PRECISION AS avg_gas_cost,
            MIN(t.gas_cost) AS min_gas_cost,
            MAX(t.gas_cost) AS max_gas_cost,
            MIN(t.block_index) AS first_block,
            MAX(t.block_index) AS last_block,
            n.gas_base
        FROM syscall_traces t
        LEFT JOIN syscall_names n ON n.hash = t.syscall_hash
        WHERE t.block_index >= start_block
          AND t.block_index <= end_block
          AND (contract_hash IS NULL OR t.contract_hash = contract_hash)
          AND (transaction_hash IS NULL OR t.tx_hash = transaction_hash)
          AND (syscall_name IS NULL OR t.syscall_name = syscall_name)
        GROUP BY t.syscall_hash, t.syscall_name, n.category, n.gas_base
    )
    SELECT aggregated.*, COUNT(*) OVER() AS total_rows
    FROM aggregated
    ORDER BY call_count DESC
    LIMIT limit_rows
    OFFSET offset_rows;
$$;

GRANT EXECUTE ON FUNCTION get_syscall_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) TO anon, authenticated;

-- ============================================
-- Opcode stats RPC
-- ============================================

CREATE OR REPLACE FUNCTION get_opcode_stats(
    start_block INTEGER,
    end_block INTEGER,
    contract_hash TEXT DEFAULT NULL,
    transaction_hash TEXT DEFAULT NULL,
    opcode INTEGER DEFAULT NULL,
    opcode_name TEXT DEFAULT NULL,
    limit_rows INTEGER DEFAULT 100,
    offset_rows INTEGER DEFAULT 0
)
RETURNS TABLE (
    opcode INTEGER,
    opcode_name TEXT,
    call_count BIGINT,
    total_gas_consumed BIGINT,
    avg_gas_consumed DOUBLE PRECISION,
    min_gas_consumed BIGINT,
    max_gas_consumed BIGINT,
    first_block INTEGER,
    last_block INTEGER,
    total_rows BIGINT
)
LANGUAGE sql
STABLE
AS $$
    WITH aggregated AS (
        SELECT
            t.opcode::INTEGER AS opcode,
            t.opcode_name,
            COUNT(*) AS call_count,
            COALESCE(SUM(t.gas_consumed), 0) AS total_gas_consumed,
            AVG(t.gas_consumed)::DOUBLE PRECISION AS avg_gas_consumed,
            MIN(t.gas_consumed) AS min_gas_consumed,
            MAX(t.gas_consumed) AS max_gas_consumed,
            MIN(t.block_index) AS first_block,
            MAX(t.block_index) AS last_block
        FROM opcode_traces t
        WHERE t.block_index >= start_block
          AND t.block_index <= end_block
          AND (contract_hash IS NULL OR t.contract_hash = contract_hash)
          AND (transaction_hash IS NULL OR t.tx_hash = transaction_hash)
          AND (opcode IS NULL OR t.opcode = opcode)
          AND (opcode_name IS NULL OR t.opcode_name = opcode_name)
        GROUP BY t.opcode, t.opcode_name
    )
    SELECT aggregated.*, COUNT(*) OVER() AS total_rows
    FROM aggregated
    ORDER BY call_count DESC
    LIMIT limit_rows
    OFFSET offset_rows;
$$;

GRANT EXECUTE ON FUNCTION get_opcode_stats(INTEGER, INTEGER, TEXT, TEXT, INTEGER, TEXT, INTEGER, INTEGER) TO anon, authenticated;
