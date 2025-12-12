import type { ContractCallStat } from '../../types';

function formatGas(gasConsumed: number) {
  const gas = gasConsumed / 1e8;
  if (gas >= 1) return `${gas.toFixed(3)} GAS`;
  return `${gas.toFixed(6)} GAS`;
}

function formatHash(hash: string) {
  if (hash.length <= 16) return hash;
  return `${hash.slice(0, 10)}…${hash.slice(-6)}`;
}

export interface ContractCallStatsTableProps {
  stats?: ContractCallStat[];
  isLoading?: boolean;
  error?: string | null;
}

export function ContractCallStatsTable({ stats = [], isLoading = false, error = null }: ContractCallStatsTableProps) {
  if (isLoading) {
    return (
      <div className="flex min-h-[200px] items-center justify-center rounded-xl border border-slate-800 bg-slate-900/70">
        <span className="text-sm text-slate-400">Loading contract call statistics…</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-xl border border-rose-500/40 bg-rose-500/10 p-4 text-sm text-rose-200">
        Failed to load contract call statistics: {error}
      </div>
    );
  }

  if (!stats.length) return null;

  return (
    <div className="rounded-xl border border-slate-800 bg-slate-950/60 p-5 shadow-lg shadow-black/40">
      <h4 className="text-sm font-semibold text-slate-200">Contract call statistics</h4>
      <div className="mt-3 overflow-hidden rounded-xl border border-slate-800">
        <table className="min-w-full divide-y divide-slate-800 text-sm">
          <thead className="bg-slate-950/70 text-left text-xs uppercase tracking-wide text-slate-400">
            <tr>
              <th className="px-4 py-2">Callee</th>
              <th className="px-4 py-2">Method</th>
              <th className="px-4 py-2">Invocations</th>
              <th className="px-4 py-2">Success</th>
              <th className="px-4 py-2">Total GAS</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-900/70 text-slate-100">
            {stats.slice(0, 12).map((stat) => {
              const successRate =
                stat.callCount > 0 ? `${((stat.successCount / stat.callCount) * 100).toFixed(1)}%` : '—';
              return (
                <tr key={`${stat.calleeHash}-${stat.methodName ?? ''}-${stat.callerHash ?? ''}`}>
                  <td className="px-4 py-2 font-mono">{formatHash(stat.calleeHash)}</td>
                  <td className="px-4 py-2 font-mono">{stat.methodName ?? '—'}</td>
                  <td className="px-4 py-2">{stat.callCount.toLocaleString()}</td>
                  <td className="px-4 py-2 text-slate-300">{successRate}</td>
                  <td className="px-4 py-2 text-amber-200">{formatGas(stat.totalGasConsumed)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

