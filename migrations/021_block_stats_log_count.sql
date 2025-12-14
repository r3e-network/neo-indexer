-- Migration: 021_block_stats_log_count.sql
-- Description: Add log_count to block_stats for runtime log volume analytics.
-- Date: 2025-12-15
--
-- runtime_logs stores individual System.Runtime.Log entries. block_stats is a
-- compact per-block aggregate; adding log_count enables fast dashboards without
-- scanning the runtime_logs table.

ALTER TABLE block_stats
    ADD COLUMN IF NOT EXISTS log_count INTEGER NOT NULL DEFAULT 0;

