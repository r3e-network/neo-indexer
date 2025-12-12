import { describe, it, expect } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { OpCodeViewer } from '../components/traces/OpCodeViewer';
import type { OpCodeTraceEntry } from '../types';

const traces: OpCodeTraceEntry[] = [
  {
    blockIndex: 10,
    txHash: '0xtx1',
    contractHash: '0xaaaa1111bbbb2222cccc3333dddd4444',
    instructionPointer: 0,
    opcode: 'PUSH1',
    opcodeName: 'PUSH1',
    operand: '0x01',
    gasConsumed: 25_000000,
    stackDepth: 1,
    order: 1,
  },
  {
    blockIndex: 10,
    txHash: '0xtx1',
    contractHash: '0xbbbb2222cccc3333dddd4444eeee5555',
    instructionPointer: 4,
    opcode: 'SHA256',
    opcodeName: 'SHA256',
    operand: '0x02',
    gasConsumed: 800_000000,
    stackDepth: 5,
    order: 2,
  },
];

describe('OpCodeViewer', () => {
  it('renders opcode rows with syntax highlighting badges', () => {
    render(<OpCodeViewer traces={traces} />);

    const pushRow = screen.getByText('PUSH1').closest('tr');
    const shaRow = screen.getByText('SHA256').closest('tr');
    expect(pushRow).toBeTruthy();
    expect(shaRow).toBeTruthy();

    expect(within(pushRow!).getByText(/stack/i)).toBeInTheDocument();
    expect(within(shaRow!).getByText(/crypto/i)).toBeInTheDocument();
  });

  it('displays formatted gas consumption and stack depth indicators', () => {
    render(<OpCodeViewer traces={traces} />);

    expect(screen.getByText('0.250000 GAS')).toBeInTheDocument();
    expect(screen.getByText('8.000 GAS')).toBeInTheDocument();
    expect(screen.getByText(/depth 5/i)).toBeInTheDocument();
    expect(screen.getByText(/100%/i)).toBeInTheDocument();
  });

  it('shows loading and empty states when applicable', () => {
    const { rerender } = render(<OpCodeViewer isLoading />);
    expect(screen.getByText(/Loading opcodes/i)).toBeInTheDocument();

    rerender(<OpCodeViewer traces={[]} emptyMessage="No traces yet" />);
    expect(screen.getByText('No traces yet')).toBeInTheDocument();
  });
});
