import { NextRequest, NextResponse } from "next/server";
import { supabaseServer } from "@/lib/supabase-server";
import { jsonWithCache } from "@/lib/response";
import { rateLimit } from "@/lib/rate-limit";

export async function GET(req: NextRequest) {
  const limited = rateLimit(req, 20, 60_000);
  if (limited) return limited;

  const { searchParams } = new URL(req.url);
  const txHash = searchParams.get("tx");

  if (!txHash || txHash.length < 64) {
    return NextResponse.json({ error: "missing or invalid tx hash" }, { status: 400 });
  }

  try {
    // Fetch all contract hops and syscalls for this transaction
    const { data: traces, error } = await supabaseServer
      .from("op_traces")
      .select("step_order,contract_hash,opcode,syscall,gas_consumed")
      .eq("tx_hash", txHash)
      .in("opcode", ["SYSCALL", "CALL", "CALLT", "CALLA", "CALL_L", "CALL_I", "CALL_E", "CALL_ED", "CALL_ET", "CALL_EDT"])
      .order("step_order", { ascending: true })
      .limit(5000);

    if (error) {
      console.error("call-graph query error:", error);
      return NextResponse.json({ error: "query failed" }, { status: 500 });
    }

    if (!traces || traces.length === 0) {
      return jsonWithCache({ nodes: [], edges: [], stats: {} }, 60);
    }

    // Build call graph
    const nodes = new Map<string, { id: string; label: string; calls: number; gasUsed: number }>();
    const edges: { from: string; to: string; label: string; step: number }[] = [];
    const syscallCounts = new Map<string, number>();

    let prevContract: string | null = null;

    for (const trace of traces) {
      const contract = trace.contract_hash || "entry";

      // Track node
      if (!nodes.has(contract)) {
        nodes.set(contract, {
          id: contract,
          label: contract === "entry" ? "Entry" : `${contract.slice(0, 10)}...`,
          calls: 0,
          gasUsed: 0,
        });
      }
      const node = nodes.get(contract)!;
      node.calls++;
      node.gasUsed += trace.gas_consumed || 0;

      // Track syscalls
      if (trace.opcode === "SYSCALL" && trace.syscall) {
        const name = trace.syscall;
        syscallCounts.set(name, (syscallCounts.get(name) || 0) + 1);
      }

      // Track edges (contract transitions)
      if (prevContract && prevContract !== contract) {
        edges.push({
          from: prevContract,
          to: contract,
          label: trace.opcode,
          step: trace.step_order,
        });
      }

      prevContract = contract;
    }

    // Convert to arrays
    const nodeArray = Array.from(nodes.values());
    const syscallArray = Array.from(syscallCounts.entries())
      .map(([name, count]) => ({ name, count }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 20);

    // Deduplicate edges (keep first occurrence)
    const edgeKey = (e: typeof edges[0]) => `${e.from}->${e.to}`;
    const seenEdges = new Set<string>();
    const uniqueEdges = edges.filter((e) => {
      const key = edgeKey(e);
      if (seenEdges.has(key)) return false;
      seenEdges.add(key);
      return true;
    });

    return jsonWithCache(
      {
        nodes: nodeArray,
        edges: uniqueEdges,
        stats: {
          totalCalls: traces.length,
          uniqueContracts: nodeArray.length,
          syscalls: syscallArray,
        },
      },
      30
    );
  } catch (e) {
    console.error("call-graph error:", e);
    return NextResponse.json({ error: "internal error" }, { status: 500 });
  }
}
