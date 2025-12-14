import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { getStorageReads, stateFileExists, mockBlock } from './storageReadsTable/mocks';
import { StorageReadsTable } from '../components/StorageReadsTable';

describe('StorageReadsTable rendering', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (stateFileExists as ReturnType<typeof vi.fn>).mockResolvedValue(false);
  });

  it('renders block info header', async () => {
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

  it('shows loading state initially', async () => {
    (getStorageReads as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise(() => {}) // keep pending to stay in loading state
    );

    render(<StorageReadsTable block={mockBlock} />);

    await waitFor(() => {
      expect(screen.getByText(/loading storage reads/i)).toBeInTheDocument();
    });
  });

  it('displays storage reads in table', async () => {
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

  it('shows empty state when no reads', async () => {
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

  it('displays error message on fetch failure', async () => {
    (getStorageReads as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('Network error'));

    render(<StorageReadsTable block={mockBlock} />);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/network error/i);
    });
  });
});
