import { useEffect, useMemo, useState } from 'react';
import type { SyscallCategory, SyscallStat, SyscallTraceEntry } from '../../types';

const categoryColors: Record<SyscallCategory, string> = {
  storage: 'bg-sky-400',
  contract: 'bg-emerald-400',
  runtime: 'bg-amber-400',
  system: 'bg-rose-400',
  crypto: 'bg-violet-400',
  network: 'bg-indigo-400',
  other: 'bg-slate-500',
};

function getSyscallCategory(name: string): SyscallCategory {
  const normalized = name.toLowerCase();
  if (normalized.includes('storage')) return 'storage';
  if (normalized.includes('contract') || normalized.includes('call')) return 'contract';
  if (normalized.includes('runtime') || normalized.includes('vm')) return 'runtime';
  if (normalized.includes('policy') || normalized.includes('gas') || normalized.includes('system')) return 'system';
  if (normalized.includes('sha') || normalized.includes('check') || normalized.includes('crypto')) return 'crypto';
  if (normalized.includes('oracle') || normalized.includes('role') || normalized.includes('state.root')) return 'network';
  return 'other';
}

function formatGasValue(value: number) {
  const gas = value / 1e8;
  if (gas >= 1) return `${gas.toFixed(2)} GAS`;
  return `${gas.toFixed(5)} GAS`;
}

export interface SyscallTimelineProps {
  syscalls?: SyscallTraceEntry[];
  stats?: SyscallStat[];
  isLoading?: boolean;
  error?: string | null;
}

export function SyscallTimeline({ syscalls = [], stats = [], isLoading = false, error = null }: SyscallTimelineProps) {
  const sortedSyscalls = useMemo(
    () => [...syscalls].sort((a, b) => a.order - b.order),
    [syscalls]
  );
  const [selected, setSelected] = useState<SyscallTraceEntry | null>(null);

  useEffect(() => {
    setSelected(sortedSyscalls[0] ?? null);
  }, [sortedSyscalls]);

  const maxGas = useMemo(() => Math.max(...sortedSyscalls.map((item) => item.gasCost), 1), [sortedSyscalls]);

  const categoryBreakdown = useMemo(() => {
    return sortedSyscalls.reduce<Record<SyscallCategory, number>>((acc, syscall) => {
      const category = getSyscallCategory(syscall.syscallName);
      acc[category] = (acc[category] ?? 0) + 1;
      return acc;
    }, {} as Record<SyscallCategory, number>);
  }, [sortedSyscalls]);

  if (isLoading) {
    return (
      <div className="flex min-h-[200px] items-center justify-center rounded-xl border border-slate-800 bg-slate-900/70">
        <span className="text-sm text-slate-400">Loading syscall timeline…</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-xl border border-rose-500/40 bg-rose-500/10 p-4 text-sm text-rose-200">
        Failed to load syscall timeline: {error}
      </div>
    );
  }

  if (!sortedSyscalls.length) {
    return (
      <div className="flex min-h-[200px] flex-col items-center justify-center rounded-xl border border-dashed border-slate-800 bg-slate-900/40 text-slate-400">
        <p>No syscall traces for the selected transaction.</p>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-5 shadow-lg shadow-black/40">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h3 className="text-lg font-semibold text-white">Syscall Timeline</h3>
          <p className="text-sm text-slate-400">Ordered execution with per-syscall gas bars</p>
        </div>
        <div className="flex flex-wrap gap-2 text-xs text-slate-300">
          {Object.entries(categoryBreakdown).map(([category, count]) => (
            <span key={category} className="inline-flex items-center gap-1 rounded-full border border-slate-700/70 px-2 py-1">
              <span className={`h-2 w-2 rounded-full ${categoryColors[category as SyscallCategory]}`} />
              {category}
              <span className="text-slate-500">{count}</span>
            </span>
          ))}
        </div>
      </div>

      <div className="mt-6 overflow-x-auto">
        <div className="grid grid-flow-col auto-cols-[minmax(160px,1fr)] gap-4">
          {sortedSyscalls.map((syscall) => {
            const category = getSyscallCategory(syscall.syscallName);
            const width = Math.max(12, (syscall.gasCost / maxGas) * 100);
            const isActive = selected?.order === syscall.order;
            return (
              <button
                type="button"
                key={`${syscall.txHash}-${syscall.order}`}
                className={`rounded-2xl border px-4 py-3 text-left transition-colors ${
                  isActive ? 'border-neo-green/70 bg-neo-green/5' : 'border-slate-800 bg-slate-950/60 hover:border-slate-700'
                }`}
                onClick={() => setSelected(syscall)}
              >
                <div className="text-xs uppercase tracking-wide text-slate-500">#{syscall.order}</div>
                <div className="mt-1 font-semibold text-slate-100">{syscall.syscallName}</div>
                <div className="mt-3 flex items-center gap-2">
                  <div className="flex-1 rounded-full bg-slate-800">
                    <div
                      className={`h-2 rounded-full ${categoryColors[category]}`}
                      style={{ width: `${width}%` }}
                    />
                  </div>
                  <span className="text-xs text-slate-400">{formatGasValue(syscall.gasCost)}</span>
                </div>
              </button>
            );
          })}
        </div>
      </div>

      {selected && (
        <div className="mt-6 rounded-xl border border-slate-800 bg-slate-950/80 p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="text-xs uppercase tracking-wide text-slate-500">Selected syscall</p>
              <p className="text-lg font-semibold text-white">{selected.syscallName}</p>
            </div>
            <div className="flex gap-6 text-sm text-slate-300">
              <div>
                <p className="text-xs uppercase text-slate-500">GAS Cost</p>
                <p className="font-semibold text-amber-200">{formatGasValue(selected.gasCost)}</p>
              </div>
              <div>
                <p className="text-xs uppercase text-slate-500">Contract</p>
                <p className="font-mono text-slate-200">
                  {selected.contractHash.slice(0, 10)}…{selected.contractHash.slice(-6)}
                </p>
              </div>
            </div>
          </div>
        </div>
      )}

      {stats.length > 0 && (
        <div className="mt-6">
          <h4 className="text-sm font-semibold text-slate-200">Aggregated statistics</h4>
          <div className="mt-3 overflow-hidden rounded-xl border border-slate-800">
            <table className="min-w-full divide-y divide-slate-800 text-sm">
              <thead className="bg-slate-950/70 text-left text-xs uppercase tracking-wide text-slate-400">
                <tr>
                  <th className="px-4 py-2">Syscall</th>
                  <th className="px-4 py-2">Category</th>
                  <th className="px-4 py-2">Invocations</th>
                  <th className="px-4 py-2">Total GAS</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-900/70 text-slate-100">
                {stats.slice(0, 8).map((stat) => (
                  <tr key={stat.syscallName}>
                    <td className="px-4 py-2 font-mono">{stat.syscallName}</td>
                    <td className="px-4 py-2 capitalize">
                      <span className={`badge ${categoryColors[stat.category]} text-slate-900`}>{stat.category}</span>
                    </td>
                    <td className="px-4 py-2">{stat.callCount.toLocaleString()}</td>
                    <td className="px-4 py-2">{formatGasValue(stat.totalGas)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
