import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { StorageReadsTable } from '../components/StorageReadsTable';
import type { Block } from '../types';

// Mock the supabase service
vi.mock('../services/supabase', () => ({
  getStorageReads: vi.fn(),
  getAllStorageReads: vi.fn(),
  downloadStateFile: vi.fn(),
  stateFileExists: vi.fn(),
}));

import { getStorageReads, stateFileExists } from '../services/supabase';

const mockBlock: Block = {
  block_index: 12345,
  hash: '0xabcdef1234567890',
  timestamp_ms: 1700000000000,
  tx_count: 5,
  read_key_count: 100,
};

describe('StorageReadsTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (stateFileExists as ReturnType<typeof vi.fn>).mockImplementation(async (_blockIndex: number, _format: string) => false);
  });

  it('should render block info header', async () => {
    (getStorageReads as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [],
      count: 0,
      page: 1,
      pageSize: 50,
      hasMore: false,
    });

    render(<StorageReadsTable block={mockBlock} />);

    await waitFor(() => {
      expect(screen.getByText(/#12345/)).toBeInTheDocument();
    });
  });

  it('should show loading state initially', async () => {
    (getStorageReads as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise(() => {}) // keep pending to stay in loading state
    );

    render(<StorageReadsTable block={mockBlock} />);

    await waitFor(() => {
      expect(screen.getByText(/loading storage reads/i)).toBeInTheDocument();
    });
  });

  it('should display storage reads in table', async () => {
    (getStorageReads as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [
        {
          id: 1,
          block_index: 12345,
          contract_id: 1,
          key_base64: 'SGVsbG8=',
          value_base64: 'V29ybGQ=',
          read_order: 1,
          tx_hash: '0xtx123',
          source: 'TryGet',
          contracts: {
            contract_id: 1,
            contract_hash: '0xcontract',
            manifest_name: 'MyContract',
          },
        },
      ],
      count: 1,
      page: 1,
      pageSize: 50,
      hasMore: false,
    });

    render(<StorageReadsTable block={mockBlock} />);

    await waitFor(() => {
      expect(screen.getByText('MyContract')).toBeInTheDocument();
    });
  });

  it('should show empty state when no reads', async () => {
    (getStorageReads as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [],
      count: 0,
      page: 1,
      pageSize: 50,
      hasMore: false,
    });

    render(<StorageReadsTable block={mockBlock} />);

    await waitFor(() => {
      expect(screen.getByText(/no storage reads found/i)).toBeInTheDocument();
    });
  });

  it('should show Download Binary button when binary file exists', async () => {
    (getStorageReads as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [],
      count: 0,
      page: 1,
      pageSize: 50,
      hasMore: false,
    });
    (stateFileExists as ReturnType<typeof vi.fn>).mockImplementation(async (_blockIndex: number, format: string) =>
      format === 'bin'
    );

    render(<StorageReadsTable block={mockBlock} />);

    await waitFor(() => {
      expect(screen.getByText(/download binary/i)).toBeInTheDocument();
    });
  });

  it('should show pagination when total exceeds page size', async () => {
    (getStorageReads as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: Array.from({ length: 50 }, (_v, i) => ({
        id: i + 1,
        block_index: 12345,
        contract_id: null,
        key_base64: 'dGVzdA==',
        value_base64: 'dGVzdA==',
        read_order: i + 1,
        tx_hash: null,
        source: null,
      })),
      count: 150,
      page: 1,
      pageSize: 50,
      hasMore: true,
    });

    render(<StorageReadsTable block={mockBlock} />);

    await waitFor(() => {
      expect(screen.getByText(/page 1 of 3/i)).toBeInTheDocument();
    });
  });

  it('should display error message on fetch failure', async () => {
    (getStorageReads as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('Network error'));

    render(<StorageReadsTable block={mockBlock} />);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/network error/i);
    });
  });
});
