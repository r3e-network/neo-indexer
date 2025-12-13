-- Migration: 012_storage_reads_idempotent_upsert.sql
-- Description: Make storage_reads upserts idempotent by adding a unique index on (block_index, contract_id, key_base64).
-- Date: 2025-12-13
--
-- Why:
-- - REST-mode uploads previously deleted storage_reads for a block then inserted new rows.
--   This is non-atomic over HTTP and can create transient gaps if an upload fails mid-way.
-- - A unique index enables ON CONFLICT upserts so resync writes can be idempotent without deletes.
--
-- Notes:
-- - This index can be large on mainnet. Apply during low-traffic periods for existing deployments.
-- - We keep the existing BIGSERIAL id PK for compatibility.

-- Note: This migration does not attempt automatic de-duplication of existing data.
-- If the unique index creation fails due to duplicates, you can perform a one-time cleanup like:
--
--   DELETE FROM storage_reads a
--   USING storage_reads b
--   WHERE a.block_index = b.block_index
--     AND a.contract_id = b.contract_id
--     AND a.key_base64 = b.key_base64
--     AND a.ctid < b.ctid;
--
-- Then rerun this migration.

-- Unique index required for PostgREST on_conflict and Postgres ON CONFLICT.
CREATE UNIQUE INDEX IF NOT EXISTS ux_storage_reads_block_contract_key
    ON storage_reads (block_index, contract_id, key_base64);
