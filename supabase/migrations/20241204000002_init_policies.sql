-- Enable RLS and allow read-only access for anon role (public explorer).

ALTER TABLE blocks ENABLE ROW LEVEL SECURITY;
ALTER TABLE transactions ENABLE ROW LEVEL SECURITY;
ALTER TABLE op_traces ENABLE ROW LEVEL SECURITY;

-- Create read policies for anon (drop first to avoid conflicts)
DROP POLICY IF EXISTS blocks_read ON blocks;
DROP POLICY IF EXISTS tx_read ON transactions;
DROP POLICY IF EXISTS trace_read ON op_traces;

CREATE POLICY blocks_read ON blocks FOR SELECT TO anon USING (true);
CREATE POLICY tx_read ON transactions FOR SELECT TO anon USING (true);
CREATE POLICY trace_read ON op_traces FOR SELECT TO anon USING (true);

-- Allow service_role full access for data ingestion
DROP POLICY IF EXISTS blocks_service ON blocks;
DROP POLICY IF EXISTS tx_service ON transactions;
DROP POLICY IF EXISTS trace_service ON op_traces;

CREATE POLICY blocks_service ON blocks FOR ALL TO service_role USING (true) WITH CHECK (true);
CREATE POLICY tx_service ON transactions FOR ALL TO service_role USING (true) WITH CHECK (true);
CREATE POLICY trace_service ON op_traces FOR ALL TO service_role USING (true) WITH CHECK (true);
