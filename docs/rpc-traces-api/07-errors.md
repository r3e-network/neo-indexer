# Errors

Errors follow the standard Neo RPC error format. Common errors include:

- `InvalidParams`: missing or malformed hashes, block indices, or option values.
- `UnknownBlock`: block does not exist (for index/hash lookups).
- `UnknownTransaction`: transaction not found.
- `InternalServerError`: Supabase query failed or trace storage is not configured.

