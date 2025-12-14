import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { mockFetchBlockTrace, mockFetchTransactionTrace } from './traceBrowser/mocks';
import TraceBrowser from '../pages/TraceBrowser';
import type { BlockTraceResult } from '../types';

afterEach(() => {
  vi.clearAllMocks();
});

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
    await waitFor(() => expect(mockFetchBlockTrace).toHaveBeenCalledWith(10));
    pending.reject(new Error('RPC down'));

    await waitFor(() => expect(screen.getByText(/Unable to load block trace: RPC down/i)).toBeInTheDocument());
  });

  it('validates syscall stats filter inputs', async () => {
    renderTraceBrowser();

    const fetchButton = screen.getByRole('button', { name: /fetch syscall stats/i });
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

    fireEvent.change(startInput, { target: { value: '0' } });
    fireEvent.change(endInput, { target: { value: '600000' } });
    fireEvent.click(fetchButton);
    await waitFor(() => expect(screen.getByText(/Block range too large/i)).toBeInTheDocument());

    fireEvent.change(startInput, { target: { value: '5' } });
    fireEvent.change(endInput, { target: { value: '3' } });
    fireEvent.click(fetchButton);
    await waitFor(() => expect(screen.getByText(/Enter a valid block range/i)).toBeInTheDocument());
  });
});
