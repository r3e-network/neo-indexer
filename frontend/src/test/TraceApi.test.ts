import { describe, it, expect, vi, afterEach, beforeAll, afterAll } from 'vitest';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';

const server = setupServer();

beforeAll(() => {
  server.listen();
});
afterEach(() => {
  server.resetHandlers();
  vi.clearAllMocks();
});
afterAll(() => server.close());

describe('Trace API service with MSW', () => {
  it('fetchBlockTrace normalizes nested traces', async () => {
    server.use(
      http.get(/https:\/\/test\.supabase\.co\/rest\/v1\/blocks.*/, ({ request }) => {
        const url = new URL(request.url);
        expect(url.searchParams.get('block_index')).toBe('eq.123');
        return HttpResponse.json({ hash: '0xabc' });
      }),
      http.get(/https:\/\/test\.supabase\.co\/rest\/v1\/opcode_traces.*/, ({ request }) => {
        const url = new URL(request.url);
        expect(url.searchParams.get('block_index')).toBe('eq.123');
        return HttpResponse.json([
          {
            block_index: 123,
            tx_hash: '0xtest',
            contract_hash: '0xaaaa',
            instruction_pointer: 0,
            opcode: 1,
            opcode_name: 'PUSH1',
            operand_base64: null,
            gas_consumed: 100000000,
            stack_depth: 2,
            trace_order: 1,
          },
        ]);
      }),
      http.get(/https:\/\/test\.supabase\.co\/rest\/v1\/syscall_traces.*/, ({ request }) => {
        const url = new URL(request.url);
        expect(url.searchParams.get('block_index')).toBe('eq.123');
        return HttpResponse.json([
          {
            block_index: 123,
            tx_hash: '0xtest',
            contract_hash: '0xaaaa',
            syscall_name: 'System.Storage.Get',
            syscall_hash: '0x01',
            gas_cost: 4000000,
            trace_order: 1,
          },
        ]);
      }),
      http.get(/https:\/\/test\.supabase\.co\/rest\/v1\/contract_calls.*/, ({ request }) => {
        const url = new URL(request.url);
        expect(url.searchParams.get('block_index')).toBe('eq.123');
        return HttpResponse.json([
          {
            block_index: 123,
            tx_hash: '0xtest',
            caller_hash: null,
            callee_hash: '0xbbbb',
            method_name: 'deploy',
            call_depth: 0,
            trace_order: 1,
            gas_consumed: 500000000,
            success: true,
          },
        ]);
      })
    );

    const api = await vi.importActual<typeof import('../services/api')>('../services/api');
    const result = await api.fetchBlockTrace(123);
    expect(result.transactions).toHaveLength(1);
    expect(result.transactions[0].opcodes[0].opcodeName).toBe('PUSH1');
    expect(result.transactions[0].syscalls[0].syscallName).toBe('System.Storage.Get');
    expect(result.transactions[0].contractCalls[0].calleeHash).toBe('0xbbbb');
  });

  it('fetchTransactionTrace returns normalized single transaction result', async () => {
    server.use(
      http.get(/https:\/\/test\.supabase\.co\/rest\/v1\/opcode_traces.*/, ({ request }) => {
        const url = new URL(request.url);
        expect(url.searchParams.get('tx_hash')).toBe('eq.0xtx2');
        return HttpResponse.json([
          {
            block_index: 321,
            tx_hash: '0xtx2',
            contract_hash: '0xcccc',
            instruction_pointer: 0,
            opcode: 5,
            opcode_name: 'PUSH2',
            operand_base64: null,
            gas_consumed: 9000000,
            stack_depth: 1,
            trace_order: 1,
          },
        ]);
      }),
      http.get(/https:\/\/test\.supabase\.co\/rest\/v1\/syscall_traces.*/, ({ request }) => {
        const url = new URL(request.url);
        expect(url.searchParams.get('tx_hash')).toBe('eq.0xtx2');
        return HttpResponse.json([]);
      }),
      http.get(/https:\/\/test\.supabase\.co\/rest\/v1\/contract_calls.*/, ({ request }) => {
        const url = new URL(request.url);
        expect(url.searchParams.get('tx_hash')).toBe('eq.0xtx2');
        return HttpResponse.json([]);
      })
    );

    const api = await vi.importActual<typeof import('../services/api')>('../services/api');
    const result = await api.fetchTransactionTrace('0xtx2');
    expect(result.txHash).toBe('0xtx2');
    expect(result.opcodes[0].opcodeName).toBe('PUSH2');
  });
});
