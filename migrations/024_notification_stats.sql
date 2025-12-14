-- Migration: 024_notification_stats.sql
-- Description: Supabase RPC for notification/event statistics with public range caps.
-- Date: 2025-12-15
--
-- Aggregates System.Runtime.Notify events (notifications table) over a bounded
-- block range. Useful for event dashboards and SQL-lite analytics via RPC.

CREATE OR REPLACE FUNCTION get_notification_stats(
    start_block INTEGER,
    end_block INTEGER,
    p_contract_hash TEXT DEFAULT NULL,
    p_transaction_hash TEXT DEFAULT NULL,
    p_event_name TEXT DEFAULT NULL,
    limit_rows INTEGER DEFAULT 100,
    offset_rows INTEGER DEFAULT 0
)
RETURNS TABLE (
    contract_hash TEXT,
    event_name TEXT,
    notification_count BIGINT,
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
            n.contract_hash,
            n.event_name,
            COUNT(*) AS notification_count,
            MIN(n.block_index) AS first_block,
            MAX(n.block_index) AS last_block
        FROM notifications n
        WHERE n.block_index >= start_block
          AND n.block_index <= end_block
          AND (p_contract_hash IS NULL OR n.contract_hash = p_contract_hash)
          AND (p_transaction_hash IS NULL OR n.tx_hash = p_transaction_hash)
          AND (p_event_name IS NULL OR n.event_name = p_event_name)
        GROUP BY n.contract_hash, n.event_name
    )
    SELECT aggregated.*, COUNT(*) OVER() AS total_rows
    FROM aggregated
    ORDER BY notification_count DESC
    LIMIT limit_rows
    OFFSET offset_rows;
END;
$$;

REVOKE ALL ON FUNCTION get_notification_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION get_notification_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) TO anon, authenticated, service_role;

