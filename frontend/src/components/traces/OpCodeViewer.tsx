import { useMemo } from 'react';
import type { OpCodeTraceEntry } from '../../types';

type OpCodeCategory = 'stack' | 'memory' | 'control' | 'arithmetic' | 'crypto' | 'other';

const categoryPalette: Record<OpCodeCategory, string> = {
  stack: 'bg-sky-500/10 text-sky-200 ring-1 ring-sky-500/30',
  memory: 'bg-violet-500/10 text-violet-200 ring-1 ring-violet-500/30',
  control: 'bg-rose-500/10 text-rose-200 ring-1 ring-rose-500/30',
  arithmetic: 'bg-amber-500/10 text-amber-200 ring-1 ring-amber-500/30',
  crypto: 'bg-emerald-500/10 text-emerald-200 ring-1 ring-emerald-500/30',
  other: 'bg-slate-500/10 text-slate-200 ring-1 ring-slate-500/20',
};

function getCategory(opcodeName: string): OpCodeCategory {
  const normalized = opcodeName.toLowerCase();
  if (normalized.startsWith('push') || normalized.includes('stack')) return 'stack';
  if (normalized.includes('mem') || normalized.includes('pick') || normalized.includes('roll')) return 'memory';
  if (normalized.includes('jump') || normalized.includes('call') || normalized.includes('ret')) return 'control';
  if (normalized.includes('add') || normalized.includes('sub') || normalized.includes('mul') || normalized.includes('div'))
    return 'arithmetic';
  if (normalized.includes('sha') || normalized.includes('check') || normalized.includes('crypto')) return 'crypto';
  return 'other';
}

function formatOperand(operand?: string | null) {
  if (!operand) return '—';
  if (operand.length <= 28) return operand;
  return `${operand.slice(0, 24)}…`;
}

function formatGas(gasConsumed: number) {
  const gas = gasConsumed / 1e8;
  if (gas >= 1) return `${gas.toFixed(3)} GAS`;
  return `${gas.toFixed(6)} GAS`;
}

export interface OpCodeViewerProps {
  traces?: OpCodeTraceEntry[];
  isLoading?: boolean;
  emptyMessage?: string;
}

export function OpCodeViewer({ traces = [], isLoading = false, emptyMessage = 'No opcode traces available.' }: OpCodeViewerProps) {
  const maxStackDepth = useMemo(
    () => (traces.length > 0 ? Math.max(...traces.map((trace) => trace.stackDepth)) || 1 : 1),
    [traces]
  );

  if (isLoading) {
    return (
      <div className="flex min-h-[240px] items-center justify-center rounded-xl border border-slate-800 bg-slate-900/70">
        <span className="text-sm text-slate-400">Loading opcodes…</span>
      </div>
    );
  }

  if (!traces.length) {
    return (
      <div className="flex min-h-[240px] flex-col items-center justify-center rounded-xl border border-dashed border-slate-800 bg-slate-900/40 text-center text-slate-400">
        <p className="text-sm">{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-xl border border-slate-800 bg-slate-950/60 shadow-lg shadow-black/40">
      <div className="max-h-[520px] overflow-auto">
        <table className="min-w-full divide-y divide-slate-800 font-mono text-xs">
          <thead className="bg-slate-900/80 text-[11px] uppercase tracking-wider text-slate-400">
            <tr>
              <th className="px-4 py-3 text-left">#</th>
              <th className="px-4 py-3 text-left">IP</th>
              <th className="px-4 py-3 text-left">Opcode</th>
              <th className="px-4 py-3 text-left">Operand</th>
              <th className="px-4 py-3 text-left">Contract</th>
              <th className="px-4 py-3 text-left">GAS</th>
              <th className="px-4 py-3 text-left">Stack</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-900/60 text-[13px]">
            {traces.map((trace) => {
              const category = getCategory(trace.opcodeName);
              const stackRatio = Math.min(1, trace.stackDepth / maxStackDepth);
              return (
                <tr
                  key={`${trace.txHash}-${trace.order}-${trace.instructionPointer}`}
                  className="transition-colors hover:bg-slate-900/80"
                >
                  <td className="px-4 py-2 text-slate-500">{trace.order}</td>
                  <td className="px-4 py-2 text-slate-200">{trace.instructionPointer}</td>
                  <td className="px-4 py-2">
                    <div className="flex items-center gap-2">
                      <span className={`rounded-md px-2 py-0.5 text-[11px] ${categoryPalette[category]}`}>
                        {trace.opcodeName}
                      </span>
                      <span className="text-[11px] uppercase tracking-wide text-slate-500">{category}</span>
                    </div>
                  </td>
                  <td className="px-4 py-2 text-emerald-200">{formatOperand(trace.operand)}</td>
                  <td className="px-4 py-2 text-slate-300">
                    {trace.contractHash.slice(0, 10)}…{trace.contractHash.slice(-6)}
                  </td>
                  <td className="px-4 py-2 text-amber-200">{formatGas(trace.gasConsumed)}</td>
                  <td className="px-4 py-2">
                    <div className="mb-1 flex items-center justify-between text-[11px] text-slate-400">
                      <span>depth {trace.stackDepth}</span>
                      <span>{Math.round(stackRatio * 100)}%</span>
                    </div>
                    <div className="h-2 rounded-full bg-slate-800">
                      <div
                        className="h-full rounded-full bg-emerald-400"
                        style={{ width: `${Math.max(stackRatio * 100, 1)}%` }}
                      />
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
