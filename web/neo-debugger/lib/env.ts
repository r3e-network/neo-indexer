const required = ["NEXT_PUBLIC_SUPABASE_URL", "NEXT_PUBLIC_SUPABASE_ANON_KEY"];

const missing = required.filter((k) => !process.env[k]);
if (missing.length) {
  const msg = `[env] Missing ${missing.join(", ")}; Supabase client will fail.`;
  if (process.env.NODE_ENV === "development") {
    // eslint-disable-next-line no-console
    console.warn(msg);
  } else {
    throw new Error(msg);
  }
}

const clampRefresh = (ms: number) => Math.min(60000, Math.max(5000, ms || 15000));

export const env = {
  supabaseUrl: process.env.NEXT_PUBLIC_SUPABASE_URL!,
  supabaseAnonKey: process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
  supabaseServiceRole: process.env.SUPABASE_SERVICE_ROLE_KEY,
  refreshMs: clampRefresh(Number(process.env.NEXT_PUBLIC_REFRESH_MS || "15000") || 15000),
};
