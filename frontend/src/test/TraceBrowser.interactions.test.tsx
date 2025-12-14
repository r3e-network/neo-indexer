import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { callGraphSpy, mockFetchBlockTrace, mockFetchContractCalls, opViewerSpy } from './traceBrowser/mocks';
import TraceBrowser from '../pages/TraceBrowser';
import { blockTrace, sampleContractCalls } from './fixtures/traceBrowserFixtures';

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
