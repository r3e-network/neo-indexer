-- Migration: 013_storage_reads_update_policy.sql
-- Description: Allow service_role to UPDATE storage_reads so PostgREST upserts can merge duplicates.
-- Date: 2025-12-13
--
-- Why:
-- - PostgREST upsert (`Prefer: resolution=merge-duplicates`) uses `ON CONFLICT DO UPDATE`.
-- - RLS requires an UPDATE policy for those conflict updates to succeed.

DO $policy$
BEGIN
    -- Ensure idempotency
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON storage_reads';

    -- Only allow UPDATE when JWT role is service_role.
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON storage_reads FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
END;
$policy$;

