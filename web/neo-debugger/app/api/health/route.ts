import { NextResponse } from "next/server";
import { supabaseServer } from "@/lib/supabase-server";
import { jsonWithCache } from "@/lib/response";

export async function GET() {
  try {
    const { data, error } = await supabaseServer
      .from("blocks")
      .select("index")
      .order("index", { ascending: false })
      .limit(1)
      .maybeSingle();

    // Don't throw on empty result - that's valid for a new database
    if (error && error.code !== "PGRST116") {
      console.error("health check error:", error);
      return NextResponse.json({ status: "error", error: error.message }, { status: 500 });
    }

    return jsonWithCache({
      status: "ok",
      latestBlock: data?.index ?? null,
      timestamp: Math.floor(Date.now() / 1000),
    }, 10);
  } catch (e) {
    console.error("health check exception:", e);
    return NextResponse.json({ status: "error" }, { status: 500 });
  }
}
