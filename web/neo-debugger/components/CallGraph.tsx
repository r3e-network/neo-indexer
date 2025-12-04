"use client";

import { useEffect, useState, useCallback } from "react";

type Node = { id: string; label: string; calls: number; gasUsed: number };
type Edge = { from: string; to: string; label: string; step: number };
type SyscallStat = { name: string; count: number };
type GraphData = {
  nodes: Node[];
  edges: Edge[];
  stats: {
    totalCalls: number;
    uniqueContracts: number;
    syscalls: SyscallStat[];
  };
};

export function CallGraph({ txHash }: { txHash: string }) {
  const [data, setData] = useState<GraphData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedNode, setSelectedNode] = useState<Node | null>(null);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(`/api/call-graph?tx=${encodeURIComponent(txHash)}`);
        if (!res.ok) {
          setError("Failed to load call graph");
          return;
        }
        const json = await res.json();
        setData(json);
      } catch {
        setError("Network error");
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [txHash]);

  const getNodeColor = useCallback((node: Node) => {
    if (node.id === "entry") return "bg-green-500";
    const intensity = Math.min(255, Math.floor((node.calls / 100) * 255));
    return `bg-blue-${Math.min(9, Math.floor(intensity / 28) + 4)}00`;
  }, []);

  if (loading) {
    return (
      <div className="p-6 bg-slate-900/70 rounded-lg border border-slate-800 animate-pulse">
        <div className="h-6 bg-slate-800 rounded w-1/3 mb-4"></div>
        <div className="h-64 bg-slate-800 rounded"></div>
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="p-6 bg-slate-900/70 rounded-lg border border-slate-800">
        <p className="text-red-400">{error || "No data available"}</p>
      </div>
    );
  }

  if (data.nodes.length === 0) {
    return (
      <div className="p-6 bg-slate-900/70 rounded-lg border border-slate-800">
        <p className="text-slate-400">No contract calls found in this transaction.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Stats Overview */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <StatBox label="Total Calls" value={data.stats.totalCalls} />
        <StatBox label="Unique Contracts" value={data.stats.uniqueContracts} />
        <StatBox label="Call Edges" value={data.edges.length} />
        <StatBox label="Syscall Types" value={data.stats.syscalls.length} />
      </div>

      {/* Visual Graph */}
      <div className="p-6 bg-slate-900/70 rounded-lg border border-slate-800">
        <h3 className="text-lg font-semibold text-white mb-4">Contract Call Flow</h3>
        <div className="relative min-h-[300px] overflow-x-auto">
          <div className="flex flex-wrap gap-4 items-start">
            {data.nodes.map((node, idx) => (
              <div
                key={node.id}
                onClick={() => setSelectedNode(selectedNode?.id === node.id ? null : node)}
                className={`
                  relative p-4 rounded-lg border-2 cursor-pointer transition-all
                  ${selectedNode?.id === node.id
                    ? "border-green-500 bg-slate-800 scale-105"
                    : "border-slate-700 bg-slate-900 hover:border-slate-500"
                  }
                `}
              >
                <div className="text-xs text-slate-500 mb-1">#{idx + 1}</div>
                <div className="font-mono text-sm text-green-400 truncate max-w-[120px]">
                  {node.label}
                </div>
                <div className="text-xs text-slate-400 mt-2">
                  {node.calls} calls
                </div>
                <div className="text-xs text-slate-500">
                  {formatGas(node.gasUsed)} gas
                </div>
                {/* Connection indicator */}
                {idx < data.nodes.length - 1 && (
                  <div className="absolute -right-3 top-1/2 transform -translate-y-1/2 text-slate-600">
                    &rarr;
                  </div>
                )}
              </div>
            ))}
          </div>

          {/* Edge list */}
          {data.edges.length > 0 && (
            <div className="mt-6 pt-4 border-t border-slate-800">
              <h4 className="text-sm font-semibold text-slate-400 mb-3">Call Transitions</h4>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 max-h-48 overflow-y-auto">
                {data.edges.map((edge, idx) => (
                  <div
                    key={idx}
                    className="flex items-center gap-2 text-xs bg-slate-950 rounded px-3 py-2"
                  >
                    <span className="text-slate-500">#{edge.step}</span>
                    <span className="text-blue-400 truncate max-w-[80px]">
                      {edge.from === "entry" ? "Entry" : edge.from.slice(0, 8)}
                    </span>
                    <span className="text-purple-400">{edge.label}</span>
                    <span className="text-slate-600">&rarr;</span>
                    <span className="text-green-400 truncate max-w-[80px]">
                      {edge.to === "entry" ? "Entry" : edge.to.slice(0, 8)}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Syscall Breakdown */}
      {data.stats.syscalls.length > 0 && (
        <div className="p-6 bg-slate-900/70 rounded-lg border border-slate-800">
          <h3 className="text-lg font-semibold text-white mb-4">Syscall Distribution</h3>
          <div className="space-y-2">
            {data.stats.syscalls.map((sc) => {
              const maxCount = data.stats.syscalls[0]?.count || 1;
              const width = Math.max(5, (sc.count / maxCount) * 100);
              return (
                <div key={sc.name} className="flex items-center gap-3">
                  <div className="w-48 text-xs text-slate-400 truncate" title={sc.name}>
                    {sc.name}
                  </div>
                  <div className="flex-1 h-5 bg-slate-950 rounded overflow-hidden">
                    <div
                      className="h-full bg-gradient-to-r from-blue-600 to-blue-400 rounded"
                      style={{ width: `${width}%` }}
                    />
                  </div>
                  <div className="w-12 text-right text-xs text-slate-500">{sc.count}</div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Selected Node Details */}
      {selectedNode && (
        <div className="p-6 bg-slate-900/70 rounded-lg border border-green-500/50">
          <h3 className="text-lg font-semibold text-white mb-4">Contract Details</h3>
          <div className="grid grid-cols-2 gap-4 text-sm">
            <div>
              <span className="text-slate-400">Hash:</span>
              <span className="ml-2 font-mono text-green-400 break-all">{selectedNode.id}</span>
            </div>
            <div>
              <span className="text-slate-400">Total Calls:</span>
              <span className="ml-2 text-white">{selectedNode.calls}</span>
            </div>
            <div>
              <span className="text-slate-400">Gas Used:</span>
              <span className="ml-2 text-white">{formatGas(selectedNode.gasUsed)}</span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function StatBox({ label, value }: { label: string; value: number }) {
  return (
    <div className="p-4 bg-slate-900/70 rounded-lg border border-slate-800 text-center">
      <div className="text-2xl font-mono text-white">{value.toLocaleString()}</div>
      <div className="text-xs text-slate-400 uppercase tracking-wider mt-1">{label}</div>
    </div>
  );
}

function formatGas(gas: number): string {
  if (gas >= 1_000_000_000) return `${(gas / 1_000_000_000).toFixed(2)}B`;
  if (gas >= 1_000_000) return `${(gas / 1_000_000).toFixed(2)}M`;
  if (gas >= 1_000) return `${(gas / 1_000).toFixed(2)}K`;
  return gas.toString();
}
