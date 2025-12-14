export const TRACE_TABS = [
  { id: 'opcodes', label: 'OpCode Trace' },
  { id: 'syscalls', label: 'Syscalls' },
  { id: 'callgraph', label: 'Contract Graph' },
] as const;

export type TraceTab = (typeof TRACE_TABS)[number]['id'];

export type TraceMode = 'block' | 'transaction';

export const MAX_STATS_RANGE = 500_000;
