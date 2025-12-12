import { describe, it, expect, vi, afterEach, beforeAll, afterAll } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import TraceBrowser from '../pages/TraceBrowser';
import type { BlockTraceResult, ContractCallTraceEntry, OpCodeTraceEntry, SyscallTraceEntry, TransactionTraceResult } from '../types';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';

const {
  mockFetchBlockTrace,
  mockFetchTransactionTrace,
  mockFetchContractCalls,
  mockFetchSyscallStats,
} = vi.hoisted(() => ({
  mockFetchBlockTrace: vi.fn(),
  mockFetchTransactionTrace: vi.fn(),
  mockFetchContractCalls: vi.fn(),
  mockFetchSyscallStats: vi.fn(),
}));

const opViewerSpy = vi.fn();
vi.mock('../components/traces/OpCodeViewer', () => ({
  OpCodeViewer: (props: unknown) => {
    opViewerSpy(props);
    const { traces = [], emptyMessage, isLoading } = props as { traces?: OpCodeTraceEntry[]; emptyMessage?: string; isLoading?: boolean };
    return (
      <div data-testid="opcode-viewer">
        {isLoading ? 'loading opcodes' : traces.length ? `opcodes:${traces.length}` : emptyMessage}
      </div>
    );
  },
}));

const syscallTimelineSpy = vi.fn();
vi.mock('../components/traces/SyscallTimeline', () => ({
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

const callGraphSpy = vi.fn();
vi.mock('../components/traces/CallGraph', () => ({
  CallGraph: (props: unknown) => {
    callGraphSpy(props);
    const { calls = [] } = props as { calls?: ContractCallTraceEntry[] };
    return <div data-testid="call-graph">call-graph:{calls.length}</div>;
  },
}));

vi.mock('../services/api', () => ({
  fetchBlockTrace: mockFetchBlockTrace,
  fetchTransactionTrace: mockFetchTransactionTrace,
  fetchContractCalls: mockFetchContractCalls,
  fetchSyscallStats: mockFetchSyscallStats,
}));

const server = setupServer();

const ensureTraceEnv = () => {
  Object.defineProperty(import.meta, 'env', {
    value: {
      ...(import.meta.env ?? {}),
      VITE_TRACE_RPC_URL: import.meta.env?.VITE_TRACE_RPC_URL ?? 'http://localhost:10332',
      VITE_TRACE_API_KEY: import.meta.env?.VITE_TRACE_API_KEY ?? 'test-trace-key',
    },
    configurable: true,
  });

  if (typeof process !== 'undefined' && process.env) {
    process.env.VITE_TRACE_RPC_URL = process.env.VITE_TRACE_RPC_URL ?? 'http://localhost:10332';
    process.env.VITE_TRACE_API_KEY = process.env.VITE_TRACE_API_KEY ?? 'test-trace-key';
  }
};

beforeAll(() => {
  ensureTraceEnv();
  server.listen();
});
afterEach(() => {
  server.resetHandlers();
  vi.clearAllMocks();
});
afterAll(() => server.close());

const sampleOpcodes: OpCodeTraceEntry[] = [
  {
    blockIndex: 1,
    txHash: '0xtx1',
    contractHash: '0xaaaa1111bbbb',
    instructionPointer: 0,
    opcode: 'PUSH1',
    opcodeName: 'PUSH1',
    operand: '0x01',
    gasConsumed: 2_00000000,
    stackDepth: 1,
    order: 1,
  },
  {
    blockIndex: 1,
    txHash: '0xtx1',
    contractHash: '0xbbbb2222cccc',
    instructionPointer: 4,
    opcode: 'SHA256',
    opcodeName: 'SHA256',
    operand: '0x02',
    gasConsumed: 5000000,
    stackDepth: 5,
    order: 2,
  },
];

const sampleSyscalls: SyscallTraceEntry[] = [
  {
    blockIndex: 1,
    txHash: '0xtx1',
    contractHash: '0xaaaa1111bbbb',
    syscallName: 'System.Storage.Get',
    syscallHash: '0x01',
    gasCost: 4_00000000,
    order: 1,
  },
  {
    blockIndex: 1,
    txHash: '0xtx1',
    contractHash: '0xbbbb2222cccc',
    syscallName: 'System.Contract.Call',
    syscallHash: '0x02',
    gasCost: 10_00000000,
    order: 2,
  },
];

const sampleContractCalls: ContractCallTraceEntry[] = [
  {
    blockIndex: 1,
    txHash: '0xtx1',
    callerHash: null,
    calleeHash: '0xaaaa1111bbbb',
    methodName: 'deploy',
    callDepth: 0,
    order: 1,
    gasConsumed: 10_00000000,
  },
  {
    blockIndex: 1,
    txHash: '0xtx1',
    callerHash: '0xaaaa1111bbbb',
    calleeHash: '0xbbbb2222cccc',
    methodName: 'invoke',
    callDepth: 1,
    order: 2,
    gasConsumed: 2_00000000,
  },
];

const sampleTransaction: TransactionTraceResult = {
  txHash: '0xtx1',
  blockIndex: 777,
  opcodes: sampleOpcodes,
  syscalls: sampleSyscalls,
  contractCalls: sampleContractCalls,
};

const blockTrace: BlockTraceResult = {
  blockIndex: 777,
  blockHash: '0xblock',
  transactions: [sampleTransaction],
};

function renderTraceBrowser() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <TraceBrowser />
    </QueryClientProvider>
  );
}

const deferred = <T,>() => {
  let resolve: (value: T | PromiseLike<T>) => void;
  let reject: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return {
    promise,
    resolve: resolve!,
    reject: reject!,
  };
};

const getTabButton = (label: string) => {
  const buttons = screen.getAllByRole('button');
  return buttons.find((btn) => btn.textContent?.trim() === label) as HTMLButtonElement;
};

describe('TraceBrowser selectors and navigation', () => {
  it('renders block/transaction selectors and toggles inputs', async () => {
    await act(async () => {
      renderTraceBrowser();
    });

    const blockRadio = screen.getByRole('radio', { name: /^Block$/i }) as HTMLInputElement;
    const txRadio = screen.getByRole('radio', { name: /^Transaction$/i }) as HTMLInputElement;
    expect(blockRadio).toBeChecked();
    expect(screen.getByLabelText(/block index/i)).toBeEnabled();
    expect(screen.getByLabelText(/transaction hash/i)).toBeDisabled();

    fireEvent.click(txRadio);
    await waitFor(() => expect(txRadio).toBeChecked());
    expect(screen.getByLabelText(/block index/i)).toBeDisabled();
    const txInput = screen.getByLabelText(/transaction hash/i) as HTMLInputElement;
    expect(txInput).toBeEnabled();

    fireEvent.change(txInput, { target: { value: '0xtest' } });
    fireEvent.click(screen.getByRole('button', { name: /load trace/i }));
    expect(screen.queryByText(/enter a transaction hash/i)).not.toBeInTheDocument();

    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 0));
    });
  });

  it('navigates between opcode, syscall, and call graph tabs', async () => {
    mockFetchBlockTrace.mockResolvedValue(blockTrace);
    renderTraceBrowser();

    fireEvent.change(screen.getByLabelText(/block index/i), { target: { value: '777' } });
    fireEvent.click(screen.getByRole('button', { name: /load trace/i }));

    await waitFor(() => expect(mockFetchBlockTrace).toHaveBeenCalledWith(777));
    await screen.findByTestId('opcode-viewer');
    expect(screen.getByTestId('opcode-viewer')).toHaveTextContent('opcodes:2');

    fireEvent.click(getTabButton('Syscalls'));
    expect(screen.getByTestId('syscall-timeline')).toHaveTextContent('timeline:2');

    fireEvent.click(getTabButton('Contract Graph'));
    expect(screen.getByTestId('call-graph')).toHaveTextContent('call-graph:2');
  });

  it('filters opcode traces using search, stack depth, and contract controls', async () => {
    mockFetchBlockTrace.mockResolvedValue(blockTrace);
    mockFetchContractCalls.mockResolvedValue({ contractHash: '0xaaaa1111bbbb', calls: sampleContractCalls });
    renderTraceBrowser();

    fireEvent.change(screen.getByLabelText(/block index/i), { target: { value: '777' } });
    fireEvent.click(screen.getByRole('button', { name: /load trace/i }));
    await screen.findByTestId('opcode-viewer');

    const opcodeInput = screen.getByPlaceholderText(/opcode name/i);
    fireEvent.change(opcodeInput, { target: { value: 'push' } });

    const depthSlider = screen.getByLabelText(/max stack depth/i);
    fireEvent.change(depthSlider, { target: { value: '2' } });

    const contractInput = screen.getByPlaceholderText(/0x contract hash/i);
    fireEvent.change(contractInput, { target: { value: 'aaaa' } });

	    await waitFor(() => {
	      const lastCall = opViewerSpy.mock.calls[opViewerSpy.mock.calls.length - 1];
	      expect(lastCall?.[0].traces).toHaveLength(1);
	      expect(lastCall?.[0].traces[0].opcodeName).toBe('PUSH1');
	    });

    fireEvent.click(screen.getByRole('button', { name: /load graph/i }));
    fireEvent.click(getTabButton('Contract Graph'));
    await screen.findByTestId('call-graph');

	    await waitFor(() => expect(mockFetchContractCalls).toHaveBeenCalledWith('aaaa'));
	    await waitFor(() => {
	      const callGraphProps = callGraphSpy.mock.calls[callGraphSpy.mock.calls.length - 1]?.[0] as
	        | { highlightContract?: string }
	        | undefined;
	      expect(callGraphProps?.highlightContract).toBe('aaaa');
	    });
	  });
});

describe('TraceBrowser validation and error states', () => {
  it('prevents submissions when inputs are invalid', async () => {
    renderTraceBrowser();
    const loadButton = screen.getByRole('button', { name: /load trace/i });
    const blockInput = screen.getByLabelText(/block index/i);
    fireEvent.change(blockInput, { target: { value: '-1' } });
    fireEvent.click(loadButton);
    await waitFor(() => expect(mockFetchBlockTrace).not.toHaveBeenCalled());

    fireEvent.click(screen.getByRole('radio', { name: /transaction/i }));
    fireEvent.click(loadButton);
    await waitFor(() => expect(mockFetchTransactionTrace).not.toHaveBeenCalled());
    await waitFor(() => expect(screen.getByText(/Enter a transaction hash/i)).toBeInTheDocument());
  });

  it('displays loading indicator and propagates fetch errors', async () => {
    const pending = deferred<BlockTraceResult>();
    mockFetchBlockTrace.mockReturnValueOnce(pending.promise);
    renderTraceBrowser();

    fireEvent.change(screen.getByLabelText(/block index/i), { target: { value: '10' } });
    fireEvent.click(screen.getByRole('button', { name: /load trace/i }));

    await screen.findByRole('button', { name: /loading/i });
    pending.reject(new Error('RPC down'));

    await waitFor(() =>
      expect(screen.getByText(/Unable to load block trace: RPC down/i)).toBeInTheDocument()
    );
  });

  it('validates syscall stats filter inputs', async () => {
    renderTraceBrowser();

    const fetchButton = screen.getByRole('button', { name: /fetch stats/i });
    const startInput = screen.getByPlaceholderText(/start block/i);
    const endInput = screen.getByPlaceholderText(/end block/i);

    fireEvent.change(startInput, { target: { value: '-1' } });
    fireEvent.change(endInput, { target: { value: '5' } });
    fireEvent.click(fetchButton);
    await waitFor(() => expect(screen.getByText(/Enter a valid block range/i)).toBeInTheDocument());

    fireEvent.change(startInput, { target: { value: '1' } });
    fireEvent.change(endInput, { target: { value: '2' } });
    fireEvent.click(fetchButton);
    await waitFor(() => expect(screen.queryByText(/Enter a valid block range/i)).not.toBeInTheDocument());

    fireEvent.change(startInput, { target: { value: '5' } });
    fireEvent.change(endInput, { target: { value: '3' } });
    fireEvent.click(fetchButton);
    await waitFor(() => expect(screen.getByText(/Enter a valid block range/i)).toBeInTheDocument());
  });
});

describe('Trace API service with MSW', () => {
  it('fetchBlockTrace normalizes nested traces', async () => {
    server.use(
      http.post('http://localhost:10332', async ({ request }) => {
        const body = await request.json();
        expect(body.method).toBe('getblocktrace');
        expect(body.params).toEqual([123]);
        return HttpResponse.json({
          jsonrpc: '2.0',
          id: 1,
          result: {
            block_index: 123,
            block_hash: '0xabc',
            transactions: [
              {
                tx_hash: '0xtest',
                block_index: 123,
                opcodes: [
                  {
                    contract_hash: '0xaaaa',
                    opcode: 'PUSH1',
                    gas_consumed: 100000000,
                    stack_depth: 2,
                    order: 1,
                  },
                ],
                syscalls: [
                  {
                    contract_hash: '0xaaaa',
                    syscall_name: 'System.Storage.Get',
                    gas_cost: 4000000,
                    order: 1,
                  },
                ],
                contract_calls: [
                  {
                    caller_hash: null,
                    callee_hash: '0xbbbb',
                    method_name: 'deploy',
                    call_depth: 0,
                    gas_consumed: 500000000,
                  },
                ],
              },
            ],
          },
        });
      })
    );

    ensureTraceEnv();
    const api = await vi.importActual<typeof import('../services/api')>('../services/api');
    const result = await api.fetchBlockTrace(123);
    expect(result.transactions).toHaveLength(1);
    expect(result.transactions[0].opcodes[0].opcodeName).toBe('PUSH1');
    expect(result.transactions[0].syscalls[0].syscallName).toBe('System.Storage.Get');
    expect(result.transactions[0].contractCalls[0].calleeHash).toBe('0xbbbb');
  });

  it('fetchTransactionTrace returns normalized single transaction result', async () => {
    server.use(
      http.post('http://localhost:10332', async ({ request }) => {
        const body = await request.json();
        expect(body.method).toBe('gettransactiontrace');
        expect(body.params).toEqual(['0xtx2']);
        return HttpResponse.json({
          jsonrpc: '2.0',
          id: 2,
          result: {
            tx_hash: '0xtx2',
            block_index: 321,
            opcodes: [
              {
                contract_hash: '0xcccc',
                opcode: 5,
                opcode_name: 'PUSH2',
                gas_consumed: 9000000,
                stack_depth: 1,
              },
            ],
            syscalls: [],
            contract_calls: [],
          },
        });
      })
    );

    ensureTraceEnv();
    const api = await vi.importActual<typeof import('../services/api')>('../services/api');
    const result = await api.fetchTransactionTrace('0xtx2');
    expect(result.txHash).toBe('0xtx2');
    expect(result.opcodes[0].opcodeName).toBe('PUSH2');
  });
});
