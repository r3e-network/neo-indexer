import { NextRequest, NextResponse } from "next/server";
import { supabaseServer } from "@/lib/supabase-server";
import { jsonWithCache } from "@/lib/response";
import { rateLimit } from "@/lib/rate-limit";

export async function GET(req: NextRequest) {
  const limited = rateLimit(req, 30, 60_000);
  if (limited) return limited;

  const { searchParams } = new URL(req.url);
  const q = (searchParams.get("q") || "").trim();

  if (!q) {
    return NextResponse.json({ error: "missing query" }, { status: 400 });
  }

  const numeric = !Number.isNaN(Number(q)) && q.length < 12;

  // Build queries
  const blockQuery = numeric
    ? supabaseServer
        .from("blocks")
        .select("index,hash,timestamp,tx_count")
        .eq("index", Number(q))
        .limit(1)
    : null;

  const txQuery = supabaseServer
    .from("transactions")
    .select("hash,block_index,sender")
    .or(`hash.ilike.${q}%,sender.ilike.${q}%`)
    .limit(10);

  // Execute queries in parallel
  const [blockRes, txRes] = await Promise.all([
    blockQuery ?? Promise.resolve({ data: [] }),
    txQuery,
  ]);

  const entries = Array.from(new Set((txRes?.data || []).map((t: any) => t.sender).filter(Boolean)));

  return jsonWithCache(
    {
      blocks: blockRes?.data ?? [],
      transactions: txRes?.data ?? [],
      senders: entries.slice(0, 5),
    },
    10
  );
}
