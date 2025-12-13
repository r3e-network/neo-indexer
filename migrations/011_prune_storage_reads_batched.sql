-- Migration: 011_prune_storage_reads_batched.sql
-- Description: Add batched pruning options for storage_reads to reduce long-running deletes.
-- Date: 2025-12-13
--
-- This migration introduces an overload:
--   prune_storage_reads(retention_blocks, batch_size, max_batches)
-- and redefines the legacy 1-arg function to call it.
--
-- Why: storage_reads is not partitioned; large DELETEs can be expensive. Batched deletes
-- allow operators to run pruning incrementally (e.g., via scheduled jobs).

-- ============================================
-- prune_storage_reads(retention_blocks, batch_size, max_batches)
-- ============================================

CREATE OR REPLACE FUNCTION prune_storage_reads(
    retention_blocks INTEGER,
    batch_size INTEGER,
    max_batches INTEGER
) RETURNS BIGINT
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    current_height INTEGER;
    cutoff_block INTEGER;
    deleted_total BIGINT := 0;
    deleted_batch BIGINT := 0;
    batches INTEGER := 0;
BEGIN
    IF retention_blocks IS NULL OR retention_blocks <= 0 THEN
        RAISE EXCEPTION 'retention_blocks must be positive'
            USING ERRCODE = '22023';
    END IF;
    IF batch_size IS NULL OR batch_size <= 0 THEN
        RAISE EXCEPTION 'batch_size must be positive'
            USING ERRCODE = '22023';
    END IF;
    IF max_batches IS NULL OR max_batches < 0 THEN
        RAISE EXCEPTION 'max_batches must be >= 0'
            USING ERRCODE = '22023';
    END IF;

    SELECT COALESCE(MAX(block_index), 0) INTO current_height FROM blocks;
    cutoff_block := current_height - retention_blocks;

    IF cutoff_block <= 0 THEN
        RETURN 0;
    END IF;

    LOOP
        EXIT WHEN max_batches > 0 AND batches >= max_batches;

        DELETE FROM storage_reads
        WHERE ctid IN (
            SELECT ctid
            FROM storage_reads
            WHERE block_index < cutoff_block
            LIMIT batch_size
        );

        GET DIAGNOSTICS deleted_batch = ROW_COUNT;
        deleted_total := deleted_total + deleted_batch;
        batches := batches + 1;

        EXIT WHEN deleted_batch = 0;
    END LOOP;

    RETURN deleted_total;
END;
$$;

COMMENT ON FUNCTION prune_storage_reads(INTEGER, INTEGER, INTEGER) IS
'Delete rows from storage_reads older than the given retention window (in blocks) using batched deletes. max_batches=0 runs until complete.';

-- ============================================
-- prune_storage_reads(retention_blocks) wrapper
-- ============================================

CREATE OR REPLACE FUNCTION prune_storage_reads(retention_blocks INTEGER)
RETURNS BIGINT
LANGUAGE sql
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT prune_storage_reads(retention_blocks, 50000, 0);
$$;

COMMENT ON FUNCTION prune_storage_reads(INTEGER) IS
'Delete rows from storage_reads older than the given retention window (in blocks). Uses batched deletes internally.';

-- ============================================
-- Function Privileges (idempotent)
-- ============================================

REVOKE ALL ON FUNCTION prune_storage_reads(INTEGER) FROM PUBLIC;
REVOKE ALL ON FUNCTION prune_storage_reads(INTEGER, INTEGER, INTEGER) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION prune_storage_reads(INTEGER) TO service_role, postgres;
GRANT EXECUTE ON FUNCTION prune_storage_reads(INTEGER, INTEGER, INTEGER) TO service_role, postgres;

