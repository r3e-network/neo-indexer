import { NextResponse } from "next/server";
import { supabaseServer } from "@/lib/supabase-server";
import { jsonWithCache } from "@/lib/response";

export async function GET() {
  try {
    const { data: latest } = await supabaseServer
      .from("blocks")
      .select("index")
      .order("index", { ascending: false })
      .limit(1)
      .maybeSingle();

    if (!latest?.index) {
      return NextResponse.json({ points: [] });
    }

    const start = Math.max(0, Number(latest.index) - 20);

    const { data: counts, error } = await supabaseServer
      .from("op_traces")
      .select("block_index,count:count()", { head: false })
      .gte("block_index", start)
      .order("block_index", { ascending: true });

    if (error) throw error;

    return jsonWithCache({
      latest: latest.index,
      points: counts ?? [],
    }, 15);
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "failed to load opcode volume" }, { status: 500 });
  }
}
