"use client";

import { useState, useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import { Search, Loader2 } from "lucide-react";
import Link from "next/link";

type StatsLatestBlock = { index: number; timestamp: number; tx_count: number };
type StatsData = {
  opTraces: number;
  latestBlock?: StatsLatestBlock | null;
  transactions: number;
  ingestionLagSeconds: number | null;
  recentBlocks?: StatsLatestBlock[];
};
type SuggestionBlock = { index: number; hash: string; timestamp: number; tx_count: number };
type SuggestionTx = { hash: string; block_index: number; sender: string | null };
type Suggestions = { blocks: SuggestionBlock[]; transactions: SuggestionTx[]; senders: string[] };

export default function Home() {
  const [query, setQuery] = useState("");
  const [stats, setStats] = useState<StatsData>();
  const [volume, setVolume] = useState<{ block_index: number; count: number }[]>([]);
  const [recentOps, setRecentOps] = useState<
    { block_index: number; tx_hash: string; step_order: number; opcode: string; syscall: string | null; contract_hash: string | null; gas_consumed: number | null }[]
  >([]);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const envRefresh = Number(process.env.NEXT_PUBLIC_REFRESH_MS || "15000") || 15000;
  const [refreshMs, setRefreshMs] = useState(clampRefresh(envRefresh));
  const [paused, setPaused] = useState(false);
  const router = useRouter();
  const aborter = useRef<AbortController | null>(null);
  const [suggestions, setSuggestions] = useState<Suggestions>({ blocks: [], transactions: [], senders: [] });
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const hexRegex = /^0x[a-fA-F0-9]{64}$/;

  useEffect(() => {
    try {
      const saved = localStorage.getItem("neo-last-query");
      if (saved) setQuery(saved);
    } catch {
      // ignore storage errors
    }
  }, []);

  useEffect(() => {
    try {
      localStorage.setItem("neo-last-query", query);
    } catch {
      // ignore storage errors
    }
  }, [query]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (!query) return;

    setSearchError(null);

    // Tx hash (0x + 64 hex) or block index.
    if (hexRegex.test(query) && query.length >= 66) {
      router.push(`/trace/${query}`);
    } else if (!isNaN(Number(query))) {
      router.push(`/block/${query}`);
    } else if (suggestions.transactions[0]) {
      router.push(`/trace/${suggestions.transactions[0].hash}`);
    } else {
      // Fallback only if looks like hex
      if (hexRegex.test(query)) {
        router.push(`/trace/${query}`);
      } else {
        setSearchError("Enter a block index or tx hash (0x...).");
      }
    }
  };

  // Typeahead search
  useEffect(() => {
    const controller = new AbortController();
    const run = async () => {
      if (!query || query.length < 3) {
        setSuggestions({ blocks: [], transactions: [], senders: [] });
        setSearchError(null);
        return;
      }
      setSearchLoading(true);
      try {
        const res = await fetch(`/api/search?q=${encodeURIComponent(query)}`, { signal: controller.signal, cache: "no-store" });
        if (!res.ok) return;
        const data = await res.json();
        setSuggestions({ blocks: data.blocks || [], transactions: data.transactions || [], senders: data.senders || [] });
      } catch {
        // ignore
      } finally {
        setSearchLoading(false);
      }
    };
    const id = setTimeout(run, 200);
    return () => {
      clearTimeout(id);
      controller.abort();
    };
  }, [query]);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        aborter.current?.abort();
        const controller = new AbortController();
        aborter.current = controller;
        const [statsRes, volumeRes, opsRes] = await Promise.all([
          fetch("/api/stats", { signal: controller.signal, cache: "no-store" }),
          fetch("/api/opcode-volume", { signal: controller.signal, cache: "no-store" }),
          fetch("/api/live-opcodes", { signal: controller.signal, cache: "no-store" }),
        ]);

        if (statsRes.ok) {
          const data = await statsRes.json();
          if (!cancelled) setStats(data);
        }
        if (volumeRes.ok) {
          const data = await volumeRes.json();
          if (!cancelled) setVolume(data.points || []);
        }
        if (opsRes.ok) {
          const data = await opsRes.json();
          if (!cancelled) setRecentOps(data.rows || []);
        }
        if (!cancelled) setLastUpdated(new Date());
      } catch {
        // ignore errors to keep UI responsive
      }
    };

    load();
    if (paused) return () => { cancelled = true; };
    const id = setInterval(load, refreshMs);
    return () => {
      cancelled = true;
      clearInterval(id);
      aborter.current?.abort();
    };
  }, [refreshMs, paused]);

  useEffect(() => {
    try {
      const storedRefresh = localStorage.getItem("neo-refresh-ms");
      const storedPaused = localStorage.getItem("neo-refresh-paused");
      if (storedRefresh) setRefreshMs(clampRefresh(Number(storedRefresh)));
      if (storedPaused === "true") setPaused(true);
    } catch {
      // ignore storage errors
    }
  }, []);

  useEffect(() => {
    try {
      localStorage.setItem("neo-refresh-ms", String(refreshMs));
      localStorage.setItem("neo-refresh-paused", paused ? "true" : "false");
    } catch {
      // ignore storage errors
    }
  }, [refreshMs, paused]);

  const latestHeight = stats?.latestBlock?.index;
  const latestTime = stats?.latestBlock?.timestamp
    ? new Date(Number(stats.latestBlock.timestamp) * 1000).toLocaleString()
    : undefined;
  const recentBlocks = stats?.recentBlocks ?? [];
  const lag = stats?.ingestionLagSeconds;

  return (
    <main className="flex min-h-screen flex-col items-center justify-center p-10 sm:p-24 text-white">
      <div className="text-center max-w-3xl space-y-6">
        <div className="space-y-2">
          <p className="text-sm uppercase tracking-[0.4em] text-green-400">Neo N3 Mainnet</p>
          <h1 className="text-5xl sm:text-6xl font-black text-transparent bg-clip-text bg-gradient-to-r from-green-400 to-blue-400 drop-shadow-lg">
            Deep Opcode Trace
          </h1>
          <p className="text-slate-400">
            Search a transaction hash to inspect every opcode, contract hop, syscall, and gas delta in seconds.
          </p>
        </div>

        <form onSubmit={handleSearch} className="w-full max-w-2xl relative mx-auto">
          <input
            type="text"
            placeholder="Search tx hash (0x...) or block height"
            className="w-full p-4 pr-12 rounded-xl bg-slate-900/80 border border-slate-700 focus:border-green-500 outline-none text-lg shadow-lg"
            value={query}
            onChange={(e) => setQuery(e.target.value.trim())}
          />
          <button type="submit" className="absolute right-4 top-4 text-slate-400 hover:text-white" aria-label="Search">
            {searchLoading ? <Loader2 className="animate-spin" /> : <Search />}
          </button>
        </form>
        {searchError && <div className="text-sm text-red-400">{searchError}</div>}

        {query.length >= 3 && !searchLoading && suggestions.blocks.length + suggestions.transactions.length === 0 && (
          <div className="w-full max-w-2xl text-left text-sm text-slate-500 mt-2">
            No matches yet. Keep typing a tx hash or sender, or enter a block height.
          </div>
        )}

        {suggestions.blocks.length + suggestions.transactions.length > 0 && (
          <div className="w-full max-w-2xl bg-slate-900/80 border border-slate-800 rounded-xl shadow-xl mt-3 text-left text-sm">
            {suggestions.blocks.length > 0 && (
              <div className="border-b border-slate-800">
                <div className="px-4 py-2 text-xs uppercase text-slate-500">Blocks</div>
                {suggestions.blocks.map((b) => (
                  <Link
                    key={b.index}
                    href={`/block/${b.index}`}
                    className="flex items-center justify-between px-4 py-2 hover:bg-slate-800/60 transition-colors"
                  >
                    <span className="text-green-400 font-semibold">#{b.index}</span>
                    <span className="text-xs text-slate-500">{b.hash}</span>
                  </Link>
                ))}
              </div>
            )}
            {suggestions.transactions.length > 0 && (
              <div>
                <div className="px-4 py-2 text-xs uppercase text-slate-500">Transactions</div>
                {suggestions.transactions.map((t) => (
                  <Link
                    key={t.hash}
                    href={`/trace/${t.hash}`}
                    className="flex items-center justify-between px-4 py-2 hover:bg-slate-800/60 transition-colors"
                  >
                    <div className="flex flex-col">
                      <span className="text-green-400 font-mono text-xs">{t.hash}</span>
                      <span className="text-xs text-slate-500">Block {t.block_index}</span>
                    </div>
                    <span className="text-xs text-slate-500 truncate max-w-[140px]">{t.sender || "-"}</span>
                  </Link>
                ))}
              </div>
            )}
            {suggestions.senders.length > 0 && (
              <div className="border-t border-slate-800">
                <div className="px-4 py-2 text-xs uppercase text-slate-500">Senders</div>
                {suggestions.senders.map((s) => (
                  <div key={s} className="px-4 py-2 text-slate-400 text-xs">{s}</div>
                ))}
              </div>
            )}
          </div>
        )}

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mt-8">
          <StatCard
            title="OpCodes Recorded"
            value={stats ? formatNumber(stats.opTraces) : "—"}
            helper={stats ? "estimated count" : "loading..."}
          />
          <StatCard
            title="Current Height"
            value={latestHeight ? latestHeight.toLocaleString() : "—"}
            helper={latestTime ? `at ${latestTime}` : ""}
          />
          <StatCard
            title="Transactions"
            value={stats ? formatNumber(stats.transactions) : "—"}
            helper="estimated count"
          />
          <StatCard
            title="Ingestion Lag"
            value={lag != null ? formatDuration(lag) : "—"}
            helper={lag != null ? `${lag}s behind now` : "loading..."}
          />
        </div>
        <div className="flex flex-col sm:flex-row items-center justify-center gap-3 text-xs text-slate-500 mt-2">
          {lastUpdated ? <div>Last updated: {lastUpdated.toLocaleTimeString()}</div> : <div>Loading…</div>}
          <div className="flex items-center gap-2">
            <label className="flex items-center gap-2">
              <span>Refresh</span>
              <select
                className="bg-slate-900 border border-slate-700 rounded px-2 py-1 text-white"
                value={refreshMs}
                onChange={(e) => setRefreshMs(clampRefresh(Number(e.target.value) || 15000))}
              >
                <option value={5000}>5s</option>
                <option value={10000}>10s</option>
                <option value={15000}>15s</option>
                <option value={30000}>30s</option>
              </select>
            </label>
            <button
              className="px-3 py-1 rounded border border-slate-700 hover:border-green-500 hover:text-white transition-colors"
              onClick={() => setPaused((p) => !p)}
            >
              {paused ? "Resume" : "Pause"}
            </button>
          </div>
        </div>

        <div className="mt-10 bg-slate-900/60 border border-slate-800 rounded-2xl shadow-xl p-6 text-left">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-lg font-semibold">Recent Blocks</h2>
            <span className="text-xs text-slate-500">last {recentBlocks.length || 0}</span>
          </div>
          <div className="grid grid-cols-1 gap-3">
            {recentBlocks.length === 0 ? (
              <div className="text-slate-500 text-sm">Loading latest blocks...</div>
            ) : (
              recentBlocks.map((b) => (
                <Link
                  key={b.index}
                  href={`/block/${b.index}`}
                  className="flex items-center justify-between bg-slate-950/70 border border-slate-800 rounded-xl px-4 py-3 hover:border-green-500 transition-colors"
                >
                  <div>
                    <div className="text-green-400 font-semibold">#{b.index.toLocaleString()}</div>
                    <div className="text-xs text-slate-500">{new Date(Number(b.timestamp) * 1000).toLocaleString()}</div>
                  </div>
                  <div className="text-sm text-slate-400">TXs: {b.tx_count}</div>
                </Link>
              ))
            )}
          </div>
          {recentBlocks.length > 0 ? (
            <div className="mt-4">
              <div className="text-xs uppercase text-slate-500 mb-2">TX Volume (recent)</div>
              <BarSparkline values={recentBlocks.map((b) => b.tx_count)} />
            </div>
          ) : null}
        </div>

        <div className="mt-6 bg-slate-900/60 border border-slate-800 rounded-2xl shadow-xl p-6 text-left w-full">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-lg font-semibold">Opcode Volume (last ~20 blocks)</h2>
            <span className="text-xs text-slate-500">live estimate</span>
          </div>
          {volume.length === 0 ? (
            <div className="text-slate-500 text-sm">Loading...</div>
          ) : (
            <BarSparkline values={volume.map((p) => p.count)} labels={volume.map((p) => p.block_index.toString())} />
          )}
        </div>

        <div className="mt-6 bg-slate-900/60 border border-slate-800 rounded-2xl shadow-xl p-6 text-left w-full">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-lg font-semibold">Latest Opcodes</h2>
            <span className="text-xs text-slate-500">last {recentOps.length}</span>
          </div>
          {recentOps.length === 0 ? (
            <div className="text-slate-500 text-sm">Loading...</div>
          ) : (
            <div className="grid grid-cols-1 gap-2 max-h-96 overflow-auto pr-1">
              {recentOps.map((op) => (
                <div
                  key={`${op.block_index}-${op.tx_hash}-${op.step_order}`}
                  className="flex items-center justify-between bg-slate-950/60 border border-slate-800 rounded-lg px-3 py-2"
                >
                  <div className="flex flex-col text-xs">
                    <span className="text-green-400 font-semibold">#{op.block_index} · {op.opcode}</span>
                    <span className="text-slate-400">{op.syscall || "-"}</span>
                    <span className="text-slate-500">{short(op.contract_hash)}</span>
                  </div>
                  <div className="text-right text-xs text-slate-400">
                    <div className="text-yellow-400 font-mono">{short(op.tx_hash)}</div>
                    <div className="text-slate-500">step {op.step_order}</div>
                    <div className="text-slate-500">gas {op.gas_consumed ?? 0}</div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </main>
  );
}

function StatCard({ title, value, helper }: { title: string; value: string; helper?: string }) {
  return (
    <div className="p-5 rounded-xl bg-slate-900/70 border border-slate-800 text-center shadow-inner">
      <h3 className="text-slate-400 text-xs uppercase tracking-widest">{title}</h3>
      <p className="text-2xl font-mono mt-2">{value}</p>
      {helper ? <p className="text-[11px] text-slate-500 mt-1">{helper}</p> : null}
    </div>
  );
}

function formatNumber(n: number) {
  if (n >= 1_000_000_000) return `${(n / 1_000_000_000).toFixed(1)}B`;
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
  return n.toLocaleString();
}

function formatDuration(seconds: number) {
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ${seconds % 60}s`;
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m`;
}

function BarSparkline({ values, labels }: { values: number[]; labels?: string[] }) {
  if (values.length === 0) return null;
  const max = Math.max(...values);
  return (
    <div className="flex items-end gap-1 h-20">
      {values.map((v, i) => {
        const height = max === 0 ? 0 : Math.max(4, Math.round((v / max) * 64));
        return (
          <div
            key={i}
            className="flex-1 rounded-t bg-gradient-to-t from-green-600 to-green-400 shadow"
            style={{ height: `${height}px`, opacity: 0.9 - i * 0.05 }}
            title={`${labels ? `#${labels[i]}: ` : ""}${v} ops`}
          />
        );
      })}
    </div>
  );
}

function short(v?: string | null, take = 6) {
  if (!v) return "-";
  if (v.length <= take * 2) return v;
  return `${v.slice(0, take)}…${v.slice(-take)}`;
}

function clampRefresh(v: number) {
  return Math.min(60000, Math.max(5000, v || 15000));
}
