import { vi } from 'vitest';
import type { ContractCallTraceEntry, OpCodeTraceEntry, SyscallTraceEntry } from '../../types';

const apiMocks = vi.hoisted(() => ({
  mockFetchBlockTrace: vi.fn(),
  mockFetchContractCallStats: vi.fn(),
  mockFetchTransactionTrace: vi.fn(),
  mockFetchContractCalls: vi.fn(),
  mockFetchSyscallStats: vi.fn(),
  mockFetchOpCodeStats: vi.fn(),
}));

export const mockFetchBlockTrace = apiMocks.mockFetchBlockTrace;
export const mockFetchContractCallStats = apiMocks.mockFetchContractCallStats;
export const mockFetchTransactionTrace = apiMocks.mockFetchTransactionTrace;
export const mockFetchContractCalls = apiMocks.mockFetchContractCalls;
export const mockFetchSyscallStats = apiMocks.mockFetchSyscallStats;
export const mockFetchOpCodeStats = apiMocks.mockFetchOpCodeStats;

export const opViewerSpy = vi.fn();
vi.mock('../../components/traces/OpCodeViewer', () => ({
  OpCodeViewer: (props: unknown) => {
    opViewerSpy(props);
    const { traces = [], emptyMessage, isLoading } = props as {
      traces?: OpCodeTraceEntry[];
      emptyMessage?: string;
      isLoading?: boolean;
    };
    return <div data-testid="opcode-viewer">{isLoading ? 'loading opcodes' : traces.length ? `opcodes:${traces.length}` : emptyMessage}</div>;
  },
}));

export const syscallTimelineSpy = vi.fn();
vi.mock('../../components/traces/SyscallTimeline', () => ({
  SyscallTimeline: (props: unknown) => {
    syscallTimelineSpy(props);
    const { syscalls = [], stats = [] } = props as { syscalls?: SyscallTraceEntry[]; stats?: unknown[] };
    return (
      <div data-testid="syscall-timeline">
        timeline:{syscalls.length} stats:{stats.length}
      </div>
    );
  },
}));

export const callGraphSpy = vi.fn();
vi.mock('../../components/traces/CallGraph', () => ({
  CallGraph: (props: unknown) => {
    callGraphSpy(props);
    const { calls = [] } = props as { calls?: ContractCallTraceEntry[] };
    return <div data-testid="call-graph">call-graph:{calls.length}</div>;
  },
}));

vi.mock('../../services/api', () => ({
  fetchBlockTrace: apiMocks.mockFetchBlockTrace,
  fetchContractCallStats: apiMocks.mockFetchContractCallStats,
  fetchTransactionTrace: apiMocks.mockFetchTransactionTrace,
  fetchContractCalls: apiMocks.mockFetchContractCalls,
  fetchSyscallStats: apiMocks.mockFetchSyscallStats,
  fetchOpCodeStats: apiMocks.mockFetchOpCodeStats,
}));
