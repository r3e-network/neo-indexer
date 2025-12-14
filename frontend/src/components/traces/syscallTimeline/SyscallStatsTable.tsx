import type { SyscallStat } from '../../../types';
import { categoryColors, formatGasValue } from './helpers';

export function SyscallStatsTable({ stats }: { stats: SyscallStat[] }) {
  return (
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
  );
}

