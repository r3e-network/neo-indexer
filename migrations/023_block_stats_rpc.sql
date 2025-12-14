-- Migration: 023_block_stats_rpc.sql
-- Description: Supabase RPC for fetching block_stats over a bounded range.
-- Date: 2025-12-15
--
-- Provides a public-safe way to page through block_stats without exposing large
-- unrestricted scans.

CREATE OR REPLACE FUNCTION get_block_stats(
    start_block INTEGER,
    end_block INTEGER,
    limit_rows INTEGER DEFAULT 100,
    offset_rows INTEGER DEFAULT 0
)
RETURNS TABLE (
    block_index INTEGER,
    tx_count INTEGER,
    total_gas_consumed BIGINT,
    opcode_count INTEGER,
    syscall_count INTEGER,
    contract_call_count INTEGER,
    storage_read_count INTEGER,
    storage_write_count INTEGER,
    notification_count INTEGER,
    log_count INTEGER,
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

    IF limit_rows IS NULL OR limit_rows < 0 THEN
        limit_rows := 0;
    END IF;
    IF offset_rows IS NULL OR offset_rows < 0 THEN
        offset_rows := 0;
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
    WITH selected AS (
        SELECT
            b.block_index,
            b.tx_count,
            b.total_gas_consumed,
            b.opcode_count,
            b.syscall_count,
            b.contract_call_count,
            b.storage_read_count,
            b.storage_write_count,
            b.notification_count,
            b.log_count
        FROM block_stats b
        WHERE b.block_index >= start_block
          AND b.block_index <= end_block
    )
    SELECT selected.*, COUNT(*) OVER() AS total_rows
    FROM selected
    ORDER BY block_index ASC
    LIMIT limit_rows
    OFFSET offset_rows;
END;
$$;

REVOKE ALL ON FUNCTION get_block_stats(INTEGER, INTEGER, INTEGER, INTEGER) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION get_block_stats(INTEGER, INTEGER, INTEGER, INTEGER) TO anon, authenticated, service_role;

