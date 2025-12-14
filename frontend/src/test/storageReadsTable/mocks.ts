import { vi } from 'vitest';
import type { Block } from '../../types';

const apiMocks = vi.hoisted(() => ({
  getStorageReads: vi.fn(),
  getAllStorageReads: vi.fn(),
  downloadStateFile: vi.fn(),
  stateFileExists: vi.fn(),
}));

export const getStorageReads = apiMocks.getStorageReads;
export const getAllStorageReads = apiMocks.getAllStorageReads;
export const downloadStateFile = apiMocks.downloadStateFile;
export const stateFileExists = apiMocks.stateFileExists;

export const mockBlock: Block = {
  block_index: 12345,
  hash: '0xabcdef1234567890',
  timestamp_ms: 1700000000000,
  tx_count: 5,
  read_key_count: 100,
};

vi.mock('../../services/supabase', () => ({
  getStorageReads: apiMocks.getStorageReads,
  getAllStorageReads: apiMocks.getAllStorageReads,
  downloadStateFile: apiMocks.downloadStateFile,
  stateFileExists: apiMocks.stateFileExists,
}));

