-- Migration: 015_transaction_results_delete_policy.sql
-- Description: Allow service_role to DELETE transaction_results for reorg cleanup.
-- Date: 2025-12-15
--
-- The indexer performs per-height cleanup on chain tip reorgs when trimming is enabled.
-- transaction_results is keyed by (block_index, tx_hash), so old tx rows must be deleted
-- when the tx set changes at a given block height.

DO $policy$
BEGIN
    -- Drop any existing delete policy to ensure idempotency.
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role delete" ON transaction_results';

    -- Create DELETE policy for service role only.
    EXECUTE 'CREATE POLICY "Allow service role delete" ON transaction_results FOR DELETE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
END;
$policy$;

COMMENT ON POLICY "Allow service role delete" ON transaction_results IS
'Allow service role to delete tx outcome rows for reorg cleanup or corrections';

