import { describe, it, expect } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { CallGraph } from '../components/traces/CallGraph';
import type { ContractCallTraceEntry } from '../types';

const calls: ContractCallTraceEntry[] = [
  {
    blockIndex: 1,
    txHash: '0xtx1',
    callerHash: null,
    calleeHash: '0xaaaa',
    methodName: 'deploy',
    callDepth: 0,
    order: 1,
    gasConsumed: 50000000,
  },
  {
    blockIndex: 1,
    txHash: '0xtx1',
    callerHash: '0xaaaa',
    calleeHash: '0xbbbb',
    methodName: 'invoke',
    callDepth: 1,
    order: 2,
    gasConsumed: 75000000,
  },
];

describe('CallGraph', () => {
  it('renders D3 nodes and edges for contract calls', async () => {
    render(<CallGraph calls={calls} highlightContract="0xaaaa" />);

    const svg = screen.getByRole('img');
    await waitFor(() => {
      expect(svg.querySelectorAll('circle').length).toBeGreaterThan(0);
      expect(svg.querySelectorAll('line').length).toBeGreaterThan(0);
    });

    expect(screen.getByText(/Highlighting/)).toHaveTextContent('0xaaaa');
  });

  it('shows loading and empty states', () => {
    const { rerender } = render(<CallGraph isLoading />);
    expect(screen.getByText(/Loading contract calls/i)).toBeInTheDocument();

    rerender(<CallGraph calls={[]} />);
    expect(screen.getByText(/No contract call traces available/i)).toBeInTheDocument();
  });
});
