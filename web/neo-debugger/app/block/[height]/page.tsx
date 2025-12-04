import Link from "next/link";
import { supabaseServer } from "@/lib/supabase-server";
import { CopyButton } from "@/components/CopyButton";

export const dynamic = "force-dynamic";

type SearchParams = { [key: string]: string | string[] | undefined };

export default async function BlockPage({ params, searchParams }: { params: { height: string }; searchParams: SearchParams }) {
  const height = Number(params.height);
  if (Number.isNaN(height)) {
    return (
      <div className="min-h-screen bg-slate-950 text-slate-200 p-10">
        <Link href="/" className="text-green-400 hover:underline">&larr; Back to Search</Link>
        <p className="mt-8">Invalid block index.</p>
      </div>
    );
  }

  const senderFilter = typeof searchParams.sender === "string" ? searchParams.sender.trim() : "";
  const hashFilter = typeof searchParams.hash === "string" ? searchParams.hash.trim() : "";
  const opcodeFilter = typeof searchParams.opcode === "string" ? searchParams.opcode.trim() : "";
  const page = Math.max(1, Number(searchParams.page ?? "1") || 1);
  const pageSize = Math.min(500, Math.max(50, Number(searchParams.pageSize ?? "100") || 100));
  const offset = (page - 1) * pageSize;

  const { data: block } = await supabaseServer
    .from("blocks")
    .select("*")
    .eq("index", height)
    .single();

  let opcodeHashes: string[] | null = null;
  if (opcodeFilter) {
    const { data: opMatches } = await supabaseServer
      .from("op_traces")
      .select("tx_hash")
      .eq("block_index", height)
      .ilike("opcode", `${opcodeFilter}%`)
      .limit(1000);
    opcodeHashes = Array.from(new Set((opMatches || []).map((o) => o.tx_hash)));
  }

  let txs: any[] = [];
  let count: number | null = null;

  if (opcodeFilter && opcodeHashes && opcodeHashes.length === 0) {
    txs = [];
    count = 0;
  } else {
    let txQuery = supabaseServer
      .from("transactions")
      .select("*", { count: "exact" })
      .eq("block_index", height);

    if (senderFilter) txQuery = txQuery.ilike("sender", `${senderFilter}%`);
    if (hashFilter) txQuery = txQuery.ilike("hash", `${hashFilter}%`);
    if (opcodeHashes && opcodeHashes.length > 0) txQuery = txQuery.in("hash", opcodeHashes);

    const res = await txQuery.order("hash", { ascending: true }).range(offset, offset + pageSize - 1);
    txs = res.data || [];
    count = res.count;
  }

  if (!block) {
    return (
      <div className="min-h-screen bg-slate-950 text-slate-200 p-10">
        <Link href="/" className="text-green-400 hover:underline">&larr; Back to Search</Link>
        <p className="mt-8">Block not found or not yet indexed.</p>
      </div>
    );
  }

  const total = count ?? txs?.length ?? 0;
  const totalPages = Math.max(1, Math.ceil(Math.max(total, 1) / pageSize));

  const baseParams = new URLSearchParams();
  if (senderFilter) baseParams.set("sender", senderFilter);
  if (hashFilter) baseParams.set("hash", hashFilter);
  if (opcodeFilter) baseParams.set("opcode", opcodeFilter);
  baseParams.set("pageSize", String(pageSize));
  const pageHref = (p: number) => {
    const params = new URLSearchParams(baseParams);
    params.set("page", String(p));
    return `/block/${height}?${params.toString()}`;
  };
  const clearParam = (key: string) => {
    const params = new URLSearchParams(baseParams);
    params.delete(key);
    params.delete("page");
    const qs = params.toString();
    return qs ? `/block/${height}?${qs}` : `/block/${height}`;
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-200 font-mono p-6 sm:p-10 space-y-6">
      <Link href="/" className="text-green-400 hover:underline">&larr; Back to Search</Link>

      <div className="p-6 bg-slate-900/70 rounded-lg border border-slate-800 shadow-lg">
        <h2 className="text-xl font-bold text-white mb-4">Block {block.index}</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm">
          <div className="flex items-center gap-2 flex-wrap">
            <span>Hash:</span>
            <span className="text-yellow-500 break-all">{block.hash}</span>
            <CopyButton value={block.hash} label="block hash" />
          </div>
          <div>Timestamp: {new Date(Number(block.timestamp) * 1000).toISOString()}</div>
          <div>Transactions: {block.tx_count}</div>
        </div>
      </div>

      <div className="p-4 bg-slate-900/70 rounded-lg border border-slate-800 shadow">
        <form method="get" className="grid grid-cols-1 sm:grid-cols-5 gap-3 text-sm items-end">
          <label className="flex flex-col gap-1">
            <span className="text-slate-400">Tx hash (prefix)</span>
            <input
              name="hash"
              defaultValue={hashFilter}
              className="p-2 rounded bg-slate-950 border border-slate-800 focus:border-green-500 outline-none"
              placeholder="0xabc..."
            />
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-slate-400">Sender (prefix)</span>
            <input
              name="sender"
              defaultValue={senderFilter}
              className="p-2 rounded bg-slate-950 border border-slate-800 focus:border-green-500 outline-none"
              placeholder="0xwallet..."
            />
          </label>
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
            <span className="text-slate-400">Page size</span>
            <select
              name="pageSize"
              defaultValue={pageSize}
              className="p-2 rounded bg-slate-950 border border-slate-800 focus:border-green-500 outline-none"
            >
              <option value="50">50</option>
              <option value="100">100</option>
              <option value="200">200</option>
              <option value="500">500</option>
            </select>
          </label>
          <button
            type="submit"
            className="self-end px-4 py-2 rounded bg-green-500 text-black font-bold hover:bg-green-400 transition-colors"
          >
            Apply
          </button>
          <Link
            href={`/block/${height}`}
            className="self-end px-3 py-2 rounded border border-slate-800 hover:border-green-500 hover:text-white transition-colors text-center"
          >
            Clear
          </Link>
        </form>
        {(hashFilter || senderFilter || opcodeFilter) && (
          <div className="mt-2 text-xs text-slate-500 space-x-2">
            <span>Filters:</span>
            {hashFilter && (
              <Link
                href={clearParam("hash")}
                className="px-2 py-1 bg-slate-900 rounded border border-slate-800 hover:border-green-500"
              >
                hash: {hashFilter} ✕
              </Link>
            )}
            {senderFilter && (
              <Link
                href={clearParam("sender")}
                className="px-2 py-1 bg-slate-900 rounded border border-slate-800 hover:border-green-500"
              >
                sender: {senderFilter} ✕
              </Link>
            )}
            {opcodeFilter && (
              <Link
                href={clearParam("opcode")}
                className="px-2 py-1 bg-slate-900 rounded border border-slate-800 hover:border-green-500"
              >
                opcode: {opcodeFilter} ✕
              </Link>
            )}
          </div>
        )}
        <div className="mt-3 text-xs text-slate-500">
          {total > 0
            ? `Showing ${offset + 1} - ${offset + (txs?.length || 0)} of ${total} txs`
            : "No transactions match current filters."}
        </div>
      </div>

      {total > 0 ? (
        <>
          <div className="overflow-x-auto rounded-lg border border-slate-800 shadow-xl">
            <table className="w-full text-left text-sm">
              <thead className="bg-slate-900 text-slate-400 uppercase">
                <tr>
                  <th className="p-3">Tx Hash</th>
                  <th className="p-3">Sender</th>
                  <th className="p-3 text-right">Sys Fee</th>
                  <th className="p-3 text-right">Net Fee</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800 bg-slate-950/80">
                {(txs || []).map((tx) => (
                  <tr key={tx.hash} className="hover:bg-slate-900/70 transition-colors">
                    <td className="p-3 text-green-400">
                      <div className="flex items-center gap-2">
                        <Link href={`/trace/${tx.hash}`} className="hover:underline">{tx.hash}</Link>
                        <CopyButton value={tx.hash} label="tx hash" />
                      </div>
                    </td>
                    <td className="p-3 text-slate-400 truncate max-w-[220px]">
                      {tx.sender ? (
                        <Link
                          href={`/block/${height}?${new URLSearchParams({
                            sender: tx.sender,
                            hash: hashFilter,
                            opcode: opcodeFilter,
                            pageSize: String(pageSize),
                          }).toString()}`}
                          className="hover:underline"
                        >
                          {tx.sender}
                        </Link>
                      ) : "-"}
                    </td>
                    <td className="p-3 text-right text-slate-500">{tx.sys_fee}</td>
                    <td className="p-3 text-right text-slate-500">{tx.net_fee}</td>
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
        </>
      ) : null}
    </div>
  );
}
