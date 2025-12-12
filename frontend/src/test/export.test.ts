import { describe, it, expect } from 'vitest';
import { exportToCsv, exportToJson, base64ToHex, truncate, formatTimestamp } from '../utils/export';
import type { StorageRead, Block } from '../types';

describe('export utilities', () => {
  const mockBlock: Block = {
    block_index: 12345,
    hash: '0xabcdef123456',
    timestamp_ms: 1700000000000,
    tx_count: 5,
    read_key_count: 10,
  };

  const mockReads: StorageRead[] = [
    {
      id: 1,
      block_index: 12345,
      contract_id: 1,
      key_base64: 'SGVsbG8=', // "Hello" in base64
      value_base64: 'V29ybGQ=', // "World" in base64
      read_order: 1,
      tx_hash: '0xtx123',
      source: 'TryGet',
      contracts: {
        contract_id: 1,
        contract_hash: '0xcontract123',
        manifest_name: 'TestContract',
      },
    },
    {
      id: 2,
      block_index: 12345,
      contract_id: null,
      key_base64: 'dGVzdA==', // "test" in base64
      value_base64: 'ZGF0YQ==', // "data" in base64
      read_order: 2,
      tx_hash: null,
      source: null,
    },
  ];

  describe('base64ToHex', () => {
    it('should convert base64 to hex correctly', () => {
      expect(base64ToHex('SGVsbG8=')).toBe('48656c6c6f'); // "Hello"
      expect(base64ToHex('V29ybGQ=')).toBe('576f726c64'); // "World"
    });

    it('should return error message for invalid base64', () => {
      expect(base64ToHex('!!invalid!!')).toBe('(invalid base64)');
    });
  });

  describe('truncate', () => {
    it('should not truncate short strings', () => {
      expect(truncate('hello', 10)).toBe('hello');
    });

    it('should truncate long strings with ellipsis', () => {
      expect(truncate('hello world', 8)).toBe('hello...');
    });

    it('should handle edge case of exact length', () => {
      expect(truncate('hello', 5)).toBe('hello');
    });
  });

  describe('formatTimestamp', () => {
    it('should format timestamp correctly', () => {
      const result = formatTimestamp(1700000000000);
      expect(result).toBeTruthy();
      expect(typeof result).toBe('string');
    });
  });

  describe('exportToCsv', () => {
    it('should generate valid CSV with headers', () => {
      const csv = exportToCsv(mockReads, mockBlock);

      // Check header comment
      expect(csv).toContain('# Block 12345');
      expect(csv).toContain('0xabcdef123456');

      // Check column headers
      expect(csv).toContain('read_order,contract_id,contract_hash');

      // Check data rows
      expect(csv).toContain('TestContract');
      expect(csv).toContain('48656c6c6f'); // "Hello" in hex
    });

    it('should escape CSV fields with special characters', () => {
      const readsWithSpecialChars: StorageRead[] = [
        {
          ...mockReads[0],
          source: 'Test, with "quotes" and\nnewline',
        },
      ];

      const csv = exportToCsv(readsWithSpecialChars, mockBlock);
      // Should escape quotes and wrap in quotes
      expect(csv).toContain('"Test, with ""quotes"" and');
    });

    it('should use CRLF line endings (RFC 4180)', () => {
      const csv = exportToCsv(mockReads, mockBlock);
      expect(csv).toContain('\r\n');
    });
  });

  describe('exportToJson', () => {
    it('should generate valid JSON structure', () => {
      const json = exportToJson(mockReads, mockBlock);
      const parsed = JSON.parse(json);

      expect(parsed.block.index).toBe(12345);
      expect(parsed.block.hash).toBe('0xabcdef123456');
      expect(parsed.storageReads).toHaveLength(2);
      expect(parsed.exportedAt).toBeTruthy();
    });

    it('should include enriched key/value data', () => {
      const json = exportToJson(mockReads, mockBlock);
      const parsed = JSON.parse(json);

      const firstRead = parsed.storageReads[0];
      expect(firstRead.key.base64).toBe('SGVsbG8=');
      expect(firstRead.key.hex).toBe('48656c6c6f');
      expect(firstRead.contract.name).toBe('TestContract');
    });

    it('should handle null contract metadata', () => {
      const json = exportToJson(mockReads, mockBlock);
      const parsed = JSON.parse(json);

      const secondRead = parsed.storageReads[1];
      expect(secondRead.contract.id).toBeNull();
      expect(secondRead.contract.hash).toBeNull();
    });
  });
});
