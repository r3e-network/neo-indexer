import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { getStorageReads, stateFileExists, mockBlock } from './storageReadsTable/mocks';
import { StorageReadsTable } from '../components/StorageReadsTable';

describe('StorageReadsTable controls', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (stateFileExists as ReturnType<typeof vi.fn>).mockResolvedValue(false);
  });

  it('shows Download Binary button when binary file exists', async () => {
    (getStorageReads as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [],
      count: 0,
      page: 1,
      pageSize: 50,
      hasMore: false,
    });
    (stateFileExists as ReturnType<typeof vi.fn>).mockImplementation(async (_blockIndex: number, format: string) => format === 'bin');

    render(<StorageReadsTable block={mockBlock} />);

    await waitFor(() => {
      expect(screen.getByText(/download binary/i)).toBeInTheDocument();
    });
  });

  it('shows pagination when total exceeds page size', async () => {
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
});
