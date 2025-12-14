import type { SyscallCategory } from '../../../types';

export const categoryColors: Record<SyscallCategory, string> = {
  storage: 'bg-sky-400',
  contract: 'bg-emerald-400',
  runtime: 'bg-amber-400',
  system: 'bg-rose-400',
  crypto: 'bg-violet-400',
  network: 'bg-indigo-400',
  other: 'bg-slate-500',
};

export function getSyscallCategory(name: string): SyscallCategory {
  const normalized = name.toLowerCase();
  if (normalized.includes('storage')) return 'storage';
  if (normalized.includes('contract') || normalized.includes('call')) return 'contract';
  if (normalized.includes('runtime') || normalized.includes('vm')) return 'runtime';
  if (normalized.includes('policy') || normalized.includes('gas') || normalized.includes('system')) return 'system';
  if (normalized.includes('sha') || normalized.includes('check') || normalized.includes('crypto')) return 'crypto';
  if (normalized.includes('oracle') || normalized.includes('role') || normalized.includes('state.root')) return 'network';
  return 'other';
}

export function formatGasValue(value: number) {
  const gas = value / 1e8;
  if (gas >= 1) return `${gas.toFixed(2)} GAS`;
  return `${gas.toFixed(5)} GAS`;
}

