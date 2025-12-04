-- Enable RLS and allow read-only access for anon role (public explorer).
-- Run after schema.sql in Supabase SQL Editor.

ALTER TABLE blocks ENABLE ROW LEVEL SECURITY;
ALTER TABLE transactions ENABLE ROW LEVEL SECURITY;
ALTER TABLE op_traces ENABLE ROW LEVEL SECURITY;

-- Allow read for everyone (adjust as needed)
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE polname = 'blocks_read') THEN
    CREATE POLICY blocks_read ON blocks FOR SELECT TO anon USING (true);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE polname = 'tx_read') THEN
    CREATE POLICY tx_read ON transactions FOR SELECT TO anon USING (true);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE polname = 'trace_read') THEN
    CREATE POLICY trace_read ON op_traces FOR SELECT TO anon USING (true);
  END IF;
END$$;

-- Restrict writes to service role only (by omission; no insert/update/delete policies for anon).
