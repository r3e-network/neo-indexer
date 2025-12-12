import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SyscallTimeline } from '../components/traces/SyscallTimeline';
import type { SyscallStat, SyscallTraceEntry } from '../types';

const syscalls: SyscallTraceEntry[] = [
  {
    blockIndex: 1,
    txHash: '0xtx1',
    contractHash: '0xaaaa',
    syscallName: 'System.Storage.Get',
    gasCost: 4_00000000,
    order: 2,
  },
  {
    blockIndex: 1,
    txHash: '0xtx1',
    contractHash: '0xbbbb',
    syscallName: 'System.Contract.Call',
    gasCost: 5_00000000,
    order: 1,
  },
];

const stats: SyscallStat[] = [
  {
    syscallName: 'System.Runtime.Notify',
    callCount: 3,
    totalGas: 7_00000000,
    category: 'runtime',
  },
];

describe('SyscallTimeline', () => {
  it('renders ordered timeline entries with gas bars and selection details', () => {
    render(<SyscallTimeline syscalls={syscalls} stats={stats} />);

    expect(screen.getByText(/Syscall Timeline/)).toBeInTheDocument();
    expect(screen.getAllByText('System.Contract.Call')[0]).toBeInTheDocument();
    expect(screen.getAllByText('5.00 GAS')[0]).toBeInTheDocument();
    expect(screen.getAllByText('4.00 GAS')[0]).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /#2/i }));
    expect(screen.getByText(/Selected syscall/i)).toBeInTheDocument();
    expect(screen.getAllByText('System.Storage.Get').length).toBeGreaterThan(0);
  });

  it('shows category breakdown chips and aggregated stats table', () => {
    render(<SyscallTimeline syscalls={syscalls} stats={stats} />);

    expect(screen.getAllByText(/storage/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/contract/i).length).toBeGreaterThan(0);
    expect(screen.getByText('System.Runtime.Notify')).toBeInTheDocument();
    expect(screen.getByText('runtime')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('renders loading and error states', () => {
    const { rerender } = render(<SyscallTimeline isLoading />);
    expect(screen.getByText(/Loading syscall timeline/i)).toBeInTheDocument();

    rerender(<SyscallTimeline error="RPC down" />);
    expect(screen.getByText(/Failed to load syscall timeline/i)).toHaveTextContent('RPC down');

    rerender(<SyscallTimeline syscalls={[]} />);
    expect(screen.getByText(/No syscall traces/i)).toBeInTheDocument();
  });
});
