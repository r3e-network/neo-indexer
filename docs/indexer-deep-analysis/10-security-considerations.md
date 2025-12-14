# 10. Security considerations

- Writes require Supabase service role key in most deployments (`NEO_STATE_RECORDER__SUPABASE_KEY`).
- Query-only RPC endpoints should not expose service role keys.
  - Prefer `NEO_RPC_TRACES__SUPABASE_KEY` with an anon key and rely on RLS policies from migrations.
- Partition management functions are SECURITY DEFINER and must remain admin-only.
- Reorg cleanup (when enabled) issues per-height DELETEs and requires service-role delete policies:
  - trace tables: `migrations/009_trace_delete_policies_and_indexes.sql`
  - tx outcomes: `migrations/015_transaction_results_delete_policy.sql`
