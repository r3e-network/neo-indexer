-- Migration: 010_prune_storage_reads.sql
-- Description: Add admin-only pruning helper for storage_reads.
-- Date: 2025-12-13
--
-- storage_reads is not partitioned (unlike the trace tables), so pruning requires
-- deletes. This migration provides a SECURITY DEFINER helper function intended
-- for scheduled jobs or operator maintenance using the service role key.

-- ============================================
-- prune_storage_reads
-- ============================================

CREATE OR REPLACE FUNCTION prune_storage_reads(retention_blocks INTEGER)
RETURNS BIGINT
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    current_height INTEGER;
    cutoff_block INTEGER;
    deleted_rows BIGINT := 0;
BEGIN
    IF retention_blocks IS NULL OR retention_blocks <= 0 THEN
        RAISE EXCEPTION 'retention_blocks must be positive'
            USING ERRCODE = '22023';
    END IF;

    SELECT COALESCE(MAX(block_index), 0) INTO current_height FROM blocks;
    cutoff_block := current_height - retention_blocks;

    IF cutoff_block <= 0 THEN
        RETURN 0;
    END IF;

    DELETE FROM storage_reads
    WHERE block_index < cutoff_block;

    GET DIAGNOSTICS deleted_rows = ROW_COUNT;
    RETURN deleted_rows;
END;
$$;

COMMENT ON FUNCTION prune_storage_reads(INTEGER) IS
'Delete rows from storage_reads older than the given retention window (in blocks). Intended for admin/service_role scheduled jobs.';

-- ============================================
-- Function Privileges (idempotent)
-- ============================================

REVOKE ALL ON FUNCTION prune_storage_reads(INTEGER) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION prune_storage_reads(INTEGER) TO service_role, postgres;

