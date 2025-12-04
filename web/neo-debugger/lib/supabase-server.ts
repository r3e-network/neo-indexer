import { createClient } from "@supabase/supabase-js";
import { env } from "./env";

export const supabaseServer = createClient(env.supabaseUrl, env.supabaseServiceRole || env.supabaseAnonKey);
