-- Migration: 031_syscall_traces_success.sql
-- Description: Add syscall success flag to syscall_traces for failure analytics.
-- Date: 2025-12-15
--
-- Success is best-effort and reflects whether the syscall handler returned
-- without throwing. This enables SQL analytics such as "which syscalls fault most".

ALTER TABLE syscall_traces
    ADD COLUMN IF NOT EXISTS success BOOLEAN NOT NULL DEFAULT TRUE;

