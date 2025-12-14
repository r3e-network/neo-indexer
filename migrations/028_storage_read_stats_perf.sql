-- Migration: 028_storage_read_stats_perf.sql
-- Description: Optimize storage read stats RPC to leverage contract_id indexes.
-- Date: 2025-12-15
--
-- storage_reads is not partitioned, but it is indexed by (contract_id). This migration
-- keeps the same public interface but resolves contract_hash -> contract_id to improve
-- query plans when filtering by contractHash.

CREATE OR REPLACE FUNCTION get_storage_read_stats(
    start_block INTEGER,
    end_block INTEGER,
    p_contract_hash TEXT DEFAULT NULL,
    p_transaction_hash TEXT DEFAULT NULL,
    p_source TEXT DEFAULT NULL,
    limit_rows INTEGER DEFAULT 100,
    offset_rows INTEGER DEFAULT 0
)
RETURNS TABLE (
    contract_hash TEXT,
    read_count BIGINT,
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
    resolved_contract_id INTEGER;
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

    resolved_contract_id := NULL;
    IF p_contract_hash IS NOT NULL THEN
        SELECT c.contract_id
        INTO resolved_contract_id
        FROM contracts c
        WHERE c.contract_hash = p_contract_hash
        LIMIT 1;

        IF resolved_contract_id IS NULL THEN
            RETURN;
        END IF;
    END IF;

    RETURN QUERY
    WITH aggregated AS (
        SELECT
            r.contract_id,
            COUNT(*) AS read_count,
            MIN(r.block_index) AS first_block,
            MAX(r.block_index) AS last_block
        FROM storage_reads r
        WHERE r.block_index >= start_block
          AND r.block_index <= end_block
          AND (resolved_contract_id IS NULL OR r.contract_id = resolved_contract_id)
          AND (p_transaction_hash IS NULL OR r.tx_hash = p_transaction_hash)
          AND (p_source IS NULL OR r.source = p_source)
        GROUP BY r.contract_id
    )
    SELECT
        c.contract_hash,
        aggregated.read_count,
        aggregated.first_block,
        aggregated.last_block,
        COUNT(*) OVER() AS total_rows
    FROM aggregated
    LEFT JOIN contracts c ON c.contract_id = aggregated.contract_id
    ORDER BY read_count DESC
    LIMIT limit_rows
    OFFSET offset_rows;
END;
$$;

REVOKE ALL ON FUNCTION get_storage_read_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION get_storage_read_stats(INTEGER, INTEGER, TEXT, TEXT, TEXT, INTEGER, INTEGER) TO anon, authenticated, service_role;

