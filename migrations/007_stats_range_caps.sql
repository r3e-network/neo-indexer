-- Migration: 007_stats_range_caps.sql
-- Description: Guard stats RPC functions against abusive mainnet ranges.
-- Date: 2025-12-12
--
-- Public (anon/authenticated) callers are capped to a maximum block range and
-- response size. Service role callers are exempt.

-- Maximum inclusive range for public stats queries.
DO $$
BEGIN
    -- No-op wrapper to make the constant easy to change per migration.
END;
$$;

-- ============================================
-- Syscall stats RPC (range/limit caps)
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
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    max_range INTEGER := 500000;
    max_limit INTEGER := 1000;
    range_size INTEGER;
    jwt_role TEXT;
BEGIN
    IF start_block IS NULL OR end_block IS NULL OR start_block < 0 OR end_block < start_block THEN
        RAISE EXCEPTION 'invalid block range'
            USING ERRCODE = '22023';
    END IF;

    range_size := end_block - start_block;
    jwt_role := current_setting('request.jwt.claim.role', true);

    IF range_size > max_range AND COALESCE(jwt_role, '') <> 'service_role' THEN
        RAISE EXCEPTION 'block range too large for public stats queries (max % blocks). Narrow the range or use a service role key.',
            max_range
            USING ERRCODE = '22023';
    END IF;

    IF limit_rows > max_limit AND COALESCE(jwt_role, '') <> 'service_role' THEN
        limit_rows := max_limit;
    END IF;

    RETURN QUERY
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
END;
$$;

REVOKE ALL ON FUNCTION get_syscall_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION get_syscall_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) TO anon, authenticated, service_role;

-- ============================================
-- Opcode stats RPC (range/limit caps)
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
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    max_range INTEGER := 500000;
    max_limit INTEGER := 1000;
    range_size INTEGER;
    jwt_role TEXT;
BEGIN
    IF start_block IS NULL OR end_block IS NULL OR start_block < 0 OR end_block < start_block THEN
        RAISE EXCEPTION 'invalid block range'
            USING ERRCODE = '22023';
    END IF;

    range_size := end_block - start_block;
    jwt_role := current_setting('request.jwt.claim.role', true);

    IF range_size > max_range AND COALESCE(jwt_role, '') <> 'service_role' THEN
        RAISE EXCEPTION 'block range too large for public stats queries (max % blocks). Narrow the range or use a service role key.',
            max_range
            USING ERRCODE = '22023';
    END IF;

    IF limit_rows > max_limit AND COALESCE(jwt_role, '') <> 'service_role' THEN
        limit_rows := max_limit;
    END IF;

    RETURN QUERY
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
END;
$$;

REVOKE ALL ON FUNCTION get_opcode_stats(INTEGER, INTEGER, TEXT, TEXT, INTEGER, TEXT, INTEGER, INTEGER) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION get_opcode_stats(INTEGER, INTEGER, TEXT, TEXT, INTEGER, TEXT, INTEGER, INTEGER) TO anon, authenticated, service_role;

-- ============================================
-- Contract call stats RPC (range/limit caps)
-- ============================================

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
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    max_range INTEGER := 500000;
    max_limit INTEGER := 1000;
    range_size INTEGER;
    jwt_role TEXT;
BEGIN
    IF start_block IS NULL OR end_block IS NULL OR start_block < 0 OR end_block < start_block THEN
        RAISE EXCEPTION 'invalid block range'
            USING ERRCODE = '22023';
    END IF;

    range_size := end_block - start_block;
    jwt_role := current_setting('request.jwt.claim.role', true);

    IF range_size > max_range AND COALESCE(jwt_role, '') <> 'service_role' THEN
        RAISE EXCEPTION 'block range too large for public stats queries (max % blocks). Narrow the range or use a service role key.',
            max_range
            USING ERRCODE = '22023';
    END IF;

    IF limit_rows > max_limit AND COALESCE(jwt_role, '') <> 'service_role' THEN
        limit_rows := max_limit;
    END IF;

    RETURN QUERY
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
END;
$$;

REVOKE ALL ON FUNCTION get_contract_call_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION get_contract_call_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) TO anon, authenticated, service_role;

