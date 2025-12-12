import type { OpCodeStat } from '../../types';

function formatGas(gasConsumed: number) {
  const gas = gasConsumed / 1e8;
  if (gas >= 1) return `${gas.toFixed(3)} GAS`;
  return `${gas.toFixed(6)} GAS`;
}

export interface OpCodeStatsTableProps {
  stats?: OpCodeStat[];
  isLoading?: boolean;
  error?: string | null;
}

export function OpCodeStatsTable({ stats = [], isLoading = false, error = null }: OpCodeStatsTableProps) {
  if (isLoading) {
    return (
      <div className="flex min-h-[200px] items-center justify-center rounded-xl border border-slate-800 bg-slate-900/70">
        <span className="text-sm text-slate-400">Loading opcode statistics…</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-xl border border-rose-500/40 bg-rose-500/10 p-4 text-sm text-rose-200">
        Failed to load opcode statistics: {error}
      </div>
    );
  }

  if (!stats.length) return null;

  return (
    <div className="rounded-xl border border-slate-800 bg-slate-950/60 p-5 shadow-lg shadow-black/40">
      <h4 className="text-sm font-semibold text-slate-200">OpCode statistics</h4>
      <div className="mt-3 overflow-hidden rounded-xl border border-slate-800">
        <table className="min-w-full divide-y divide-slate-800 text-sm">
          <thead className="bg-slate-950/70 text-left text-xs uppercase tracking-wide text-slate-400">
            <tr>
              <th className="px-4 py-2">OpCode</th>
              <th className="px-4 py-2">Invocations</th>
              <th className="px-4 py-2">Total GAS</th>
              <th className="px-4 py-2">Avg GAS</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-900/70 text-slate-100">
            {stats.slice(0, 12).map((stat) => (
              <tr key={`${stat.opcode}-${stat.opcodeName}`}>
                <td className="px-4 py-2 font-mono">{stat.opcodeName}</td>
                <td className="px-4 py-2">{stat.callCount.toLocaleString()}</td>
                <td className="px-4 py-2 text-amber-200">{formatGas(stat.totalGasConsumed)}</td>
                <td className="px-4 py-2 text-slate-300">
                  {stat.averageGasConsumed !== undefined ? formatGas(stat.averageGasConsumed) : '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

