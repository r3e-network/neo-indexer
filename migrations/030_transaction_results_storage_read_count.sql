-- Migration: 030_transaction_results_storage_read_count.sql
-- Description: Add storage_read_count to transaction_results for fast per-transaction analytics.
-- Date: 2025-12-15
--
-- Note: storage_reads is deduped per (block_index, contract_id, key_base64).
-- The indexer fills storage_read_count as the number of unique keys first-observed
-- during this transaction within the block (i.e., attributed via tx_hash).

ALTER TABLE transaction_results
    ADD COLUMN IF NOT EXISTS storage_read_count INTEGER NOT NULL DEFAULT 0;

