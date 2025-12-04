import { NextResponse } from "next/server";
import { supabaseServer } from "@/lib/supabase-server";
import { jsonWithCache } from "@/lib/response";

export async function GET() {
  try {
    const { data: recent } = await supabaseServer
      .from("blocks")
      .select("index,timestamp,tx_count")
      .order("index", { ascending: false })
      .limit(5);

    const latest = recent?.[0];

    const { count: opCount } = await supabaseServer
      .from("op_traces")
      .select("tx_hash", { count: "estimated", head: true });

    const { count: txCount } = await supabaseServer
      .from("transactions")
      .select("hash", { count: "estimated", head: true });

    const nowSeconds = Math.floor(Date.now() / 1000);
    const ingestionLagSeconds = latest?.timestamp ? Math.max(0, nowSeconds - Number(latest.timestamp)) : null;

    return jsonWithCache({
      latestBlock: latest ?? null,
      recentBlocks: recent ?? [],
      ingestionLagSeconds,
      opTraces: opCount ?? 0,
      transactions: txCount ?? 0,
    }, 15);
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "failed to load stats" }, { status: 500 });
  }
}
