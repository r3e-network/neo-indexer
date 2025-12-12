-- Migration: 009_trace_delete_policies_and_indexes.sql
-- Description: Add service role DELETE policies for trace tables and missing indexes.
-- Date: 2025-12-13
--
-- This migration adds:
-- 1. DELETE policies for all trace tables (service_role only)
-- 2. method_name index on contract_calls for efficient filtering

-- ============================================
-- DELETE Policies for Trace Tables
-- ============================================
-- Trace data is immutable by design, but service role may need to
-- delete data during partition pruning or data corrections.

DO $policy$
BEGIN
    -- Drop any existing delete policies to ensure idempotency
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role delete" ON opcode_traces';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role delete" ON syscall_traces';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role delete" ON contract_calls';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role delete" ON storage_writes';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role delete" ON notifications';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role delete" ON block_stats';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role delete" ON syscall_names';

    -- Create DELETE policies for service role only
    EXECUTE 'CREATE POLICY "Allow service role delete" ON opcode_traces FOR DELETE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role delete" ON syscall_traces FOR DELETE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role delete" ON contract_calls FOR DELETE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role delete" ON storage_writes FOR DELETE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role delete" ON notifications FOR DELETE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role delete" ON block_stats FOR DELETE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role delete" ON syscall_names FOR DELETE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
END;
$policy$;

-- ============================================
-- Missing Index: contract_calls.method_name
-- ============================================
-- The get_contract_call_stats RPC supports filtering by method_name,
-- but the parent table lacked an index for this column.

CREATE INDEX IF NOT EXISTS idx_contract_calls_method ON contract_calls(method_name);

-- ============================================
-- Comments
-- ============================================
COMMENT ON POLICY "Allow service role delete" ON opcode_traces IS 'Allow service role to delete trace data for pruning or corrections';
COMMENT ON POLICY "Allow service role delete" ON syscall_traces IS 'Allow service role to delete trace data for pruning or corrections';
COMMENT ON POLICY "Allow service role delete" ON contract_calls IS 'Allow service role to delete trace data for pruning or corrections';
COMMENT ON POLICY "Allow service role delete" ON storage_writes IS 'Allow service role to delete trace data for pruning or corrections';
COMMENT ON POLICY "Allow service role delete" ON notifications IS 'Allow service role to delete trace data for pruning or corrections';
