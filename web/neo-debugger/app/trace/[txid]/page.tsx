import Link from "next/link";
import { supabaseServer } from "@/lib/supabase-server";
import { CopyButton } from "@/components/CopyButton";
import { CallGraph } from "@/components/CallGraph";

export const dynamic = "force-dynamic";

type SearchParams = { [key: string]: string | string[] | undefined };

export default async function TracePage({ params, searchParams }: { params: { txid: string }; searchParams: SearchParams }) {
  const txid = decodeURIComponent(params.txid);

  const page = Math.max(1, Number(searchParams.page ?? "1") || 1);
  const pageSize = Math.min(1000, Math.max(50, Number(searchParams.pageSize ?? "200") || 200));
  const offset = (page - 1) * pageSize;

  const opcodeFilter = typeof searchParams.opcode === "string" ? searchParams.opcode.trim() : "";
  const contractFilter = typeof searchParams.contract === "string" ? searchParams.contract.trim() : "";
  const syscallFilter = typeof searchParams.syscall === "string" ? searchParams.syscall.trim() : "";

  const { data: tx, error: txError } = await supabaseServer
    .from("transactions")
    .select("*")
    .eq("hash", txid)
    .single();

  let traceQuery = supabaseServer
    .from("op_traces")
    .select("*", { count: "exact" })
    .eq("tx_hash", txid);

  if (opcodeFilter) traceQuery = traceQuery.ilike("opcode", `${opcodeFilter}%`);
  if (contractFilter) traceQuery = traceQuery.ilike("contract_hash", `${contractFilter}%`);
  if (syscallFilter) traceQuery = traceQuery.ilike("syscall", `%${syscallFilter}%`);

  const { data: traces, error: traceError, count } = await traceQuery
    .order("step_order", { ascending: true })
    .range(offset, offset + pageSize - 1);

  const txErrorMsg = txError ? (txError as any)?.message || "Failed to load tx" : null;
  const traceErrorMsg = traceError ? (traceError as any)?.message || "Failed to load traces" : null;

  const total = count ?? traces?.length ?? 0;
  const totalPages = Math.max(1, Math.ceil((count || traces?.length || 0) / pageSize));

  if (!traces || traces.length === 0) {
    return (
      <div className="min-h-screen bg-slate-950 text-slate-200 p-10">
        <Link href="/" className="text-green-400 hover:underline">&larr; Back to Search</Link>
        <p className="mt-8 text-lg">Transaction not found or not yet indexed.</p>
        {txErrorMsg || traceErrorMsg ? <p className="mt-2 text-sm text-red-400">{txErrorMsg || traceErrorMsg}</p> : null}
      </div>
    );
  }

  const baseParams = new URLSearchParams();
  if (opcodeFilter) baseParams.set("opcode", opcodeFilter);
  if (contractFilter) baseParams.set("contract", contractFilter);
  if (syscallFilter) baseParams.set("syscall", syscallFilter);
  baseParams.set("pageSize", String(pageSize));
  const pageHref = (p: number) => {
    const params = new URLSearchParams(baseParams);
    params.set("page", String(p));
    return `/trace/${txid}?${params.toString()}`;
  };
  const clearFilter = (key: string) => {
    const params = new URLSearchParams(baseParams);
    params.delete(key);
    params.delete("page");
    const qs = params.toString();
    return qs ? `/trace/${txid}?${qs}` : `/trace/${txid}`;
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-200 font-mono p-6 sm:p-10 space-y-6">
      <Link href="/" className="text-green-400 hover:underline">&larr; Back to Search</Link>

      <div className="p-6 bg-slate-900/70 rounded-lg border border-slate-800 shadow-lg">
        <h2 className="text-xl font-bold text-white mb-4">Transaction Details</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm">
          <div className="flex items-center gap-2 flex-wrap">
            <span>Hash:</span>
            <span className="text-yellow-500 break-all">{txid}</span>
            <CopyButton value={txid} label="tx hash" />
          </div>
          <div>Block: {tx?.block_index}</div>
          <div>Sender: {tx?.sender}</div>
          <div>Fee: {(((tx?.sys_fee || 0) + (tx?.net_fee || 0)) / 100000000).toFixed(4)} GAS</div>
        </div>
      </div>

      {/* Call Graph Visualization */}
      <details className="group">
        <summary className="cursor-pointer p-4 bg-slate-900/70 rounded-lg border border-slate-800 hover:border-green-500 transition-colors">
          <span className="text-lg font-semibold text-white">Contract Call Graph</span>
          <span className="ml-2 text-slate-400 text-sm">(click to expand)</span>
        </summary>
        <div className="mt-4">
          <CallGraph txHash={txid} />
        </div>
      </details>

      <div className="p-4 bg-slate-900/70 rounded-lg border border-slate-800 shadow">
        <form method="get" className="grid grid-cols-1 sm:grid-cols-4 gap-3 text-sm items-end">
          <label className="flex flex-col gap-1">
            <span className="text-slate-400">Opcode (prefix)</span>
            <input
              name="opcode"
              defaultValue={opcodeFilter}
              className="p-2 rounded bg-slate-950 border border-slate-800 focus:border-green-500 outline-none"
              placeholder="SYSCALL / CALL"
            />
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-slate-400">Contract (prefix)</span>
            <input
              name="contract"
              defaultValue={contractFilter}
              className="p-2 rounded bg-slate-950 border border-slate-800 focus:border-green-500 outline-none"
              placeholder="0xabc..."
            />
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-slate-400">SysCall contains</span>
            <input
              name="syscall"
              defaultValue={syscallFilter}
              className="p-2 rounded bg-slate-950 border border-slate-800 focus:border-green-500 outline-none"
              placeholder="System.Contract.Call"
            />
          </label>
          <div className="flex gap-2">
            <label className="flex flex-col gap-1 flex-1">
              <span className="text-slate-400">Page size</span>
              <select
                name="pageSize"
                defaultValue={pageSize}
                className="p-2 rounded bg-slate-950 border border-slate-800 focus:border-green-500 outline-none"
              >
                <option value="100">100</option>
                <option value="200">200</option>
                <option value="500">500</option>
                <option value="1000">1000</option>
              </select>
            </label>
            <button
              type="submit"
              className="self-end px-4 py-2 rounded bg-green-500 text-black font-bold hover:bg-green-400 transition-colors"
            >
              Apply
            </button>
            <Link
              href={`/trace/${txid}`}
              className="self-end px-3 py-2 rounded border border-slate-800 hover:border-green-500 hover:text-white transition-colors text-center"
            >
              Clear
            </Link>
          </div>
        </form>
        {(opcodeFilter || contractFilter || syscallFilter) && (
          <div className="mt-3 text-xs text-slate-500 space-x-2">
            <span>Filters:</span>
            {opcodeFilter && (
              <Link
                href={clearFilter("opcode")}
                className="px-2 py-1 bg-slate-900 rounded border border-slate-800 hover:border-green-500"
              >
                opcode: {opcodeFilter} ✕
              </Link>
            )}
            {contractFilter && (
              <Link
                href={clearFilter("contract")}
                className="px-2 py-1 bg-slate-900 rounded border border-slate-800 hover:border-green-500"
              >
                contract: {contractFilter} ✕
              </Link>
            )}
            {syscallFilter && (
              <Link
                href={clearFilter("syscall")}
                className="px-2 py-1 bg-slate-900 rounded border border-slate-800 hover:border-green-500"
              >
                syscall: {syscallFilter} ✕
              </Link>
            )}
          </div>
        )}
        <div className="mt-3 text-xs text-slate-500">
          Showing {offset + 1} - {offset + traces.length} of {total} steps
        </div>
      </div>

      <div className="overflow-x-auto rounded-lg border border-slate-800 shadow-xl">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-900 text-slate-400 uppercase">
            <tr>
              <th className="p-3">#</th>
              <th className="p-3">OpCode</th>
              <th className="p-3">Contract</th>
              <th className="p-3">SysCall</th>
              <th className="p-3">Stack Top</th>
              <th className="p-3 text-right">Gas</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800 bg-slate-950/80">
            {traces.map((row) => (
              <tr key={`${row.step_order}-${row.contract_hash ?? ""}`} className="hover:bg-slate-900/70 transition-colors">
                <td className="p-3 text-slate-500">{row.step_order}</td>
                <td className={`p-3 font-bold ${getOpColor(row.opcode)}`}>{row.opcode}</td>
                <td className="p-3 text-xs text-slate-500 font-mono">
                  {row.contract_hash ? `${row.contract_hash.substring(0, 10)}...` : "-"}
                </td>
                <td className="p-3 text-blue-400">{row.syscall}</td>
                <td className="p-3 text-xs text-slate-400 truncate max-w-[240px]">{row.stack_top}</td>
                <td className="p-3 text-right text-slate-500">{row.gas_consumed}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between text-sm text-slate-400">
        <div>Page {page} / {totalPages}</div>
        <div className="flex gap-3">
          <Link
            href={page > 1 ? pageHref(page - 1) : "#"}
            className={`px-3 py-2 rounded border border-slate-800 ${page > 1 ? "hover:border-green-500 hover:text-white" : "opacity-30 cursor-not-allowed"}`}
          >
            &larr; Prev
          </Link>
          <Link
            href={page < totalPages ? pageHref(page + 1) : "#"}
            className={`px-3 py-2 rounded border border-slate-800 ${page < totalPages ? "hover:border-green-500 hover:text-white" : "opacity-30 cursor-not-allowed"}`}
          >
            Next &rarr;
          </Link>
        </div>
      </div>
    </div>
  );
}

function getOpColor(op: string) {
  if (op === "SYSCALL") return "text-blue-500";
  if (op === "RET") return "text-red-500";
  if (op.startsWith("CALL")) return "text-purple-500";
  if (op.startsWith("JMP")) return "text-orange-500";
  return "text-slate-200";
}
