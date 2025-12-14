-- Migration: 020_transaction_results_log_count.sql
-- Description: Add log_count to transaction_results for fast runtime log volume analytics.
-- Date: 2025-12-15
--
-- runtime_logs stores each System.Runtime.Log entry. This migration adds a
-- per-transaction counter to transaction_results so common analytics can be
-- answered without joining the full runtime_logs table.

ALTER TABLE transaction_results
    ADD COLUMN IF NOT EXISTS log_count INTEGER NOT NULL DEFAULT 0;

