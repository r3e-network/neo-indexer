# 10. Security considerations

- Writes require Supabase service role key in most deployments (`NEO_STATE_RECORDER__SUPABASE_KEY`).
- Query-only RPC endpoints should not expose service role keys.
  - Prefer `NEO_RPC_TRACES__SUPABASE_KEY` with an anon key and rely on RLS policies from migrations.
- Partition management functions are SECURITY DEFINER and must remain admin-only.
