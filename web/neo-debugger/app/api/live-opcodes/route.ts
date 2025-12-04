import { NextResponse } from "next/server";
import { supabaseServer } from "@/lib/supabase-server";
import { jsonWithCache } from "@/lib/response";

// Returns recent opcode events ordered by block desc then step desc.
export async function GET() {
  try {
    const { data, error } = await supabaseServer
      .from("op_traces")
      .select("block_index,tx_hash,step_order,opcode,syscall,contract_hash,gas_consumed")
      .order("block_index", { ascending: false })
      .order("step_order", { ascending: false })
      .limit(50);

    if (error) throw error;

    return jsonWithCache({ rows: data ?? [] }, 5);
  } catch (e) {
    console.error(e);
    return NextResponse.json({ error: "failed to load opcodes" }, { status: 500 });
  }
}
