import type { StorageRead, Block } from '../types';

/**
 * Escape CSV field according to RFC 4180
 */
function escapeCsvField(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return '';
  const str = String(value);
  if (str.includes('"') || str.includes(',') || str.includes('\n') || str.includes('\r')) {
    return `"${str.replace(/"/g, '""')}"`;
  }
  return str;
}

/**
 * Export storage reads to CSV format (RFC 4180 compliant)
 */
export function exportToCsv(reads: StorageRead[], block: Block): string {
  const headers = [
    'read_order',
    'contract_id',
    'contract_hash',
    'contract_name',
    'key_base64',
    'key_hex',
    'value_base64',
    'value_hex',
    'tx_hash',
    'source',
  ];

  const rows = reads.map((read) => [
    escapeCsvField(read.read_order),
    escapeCsvField(read.contract_id),
    escapeCsvField(read.contracts?.contract_hash ?? ''),
    escapeCsvField(read.contracts?.manifest_name ?? ''),
    escapeCsvField(read.key_base64),
    escapeCsvField(base64ToHex(read.key_base64)),
    escapeCsvField(read.value_base64),
    escapeCsvField(base64ToHex(read.value_base64)),
    escapeCsvField(read.tx_hash),
    escapeCsvField(read.source),
  ]);

  const csvContent = [
    `# Block ${block.block_index} - ${block.hash}`,
    `# Timestamp: ${new Date(block.timestamp_ms).toISOString()}`,
    `# Transaction Count: ${block.tx_count}`,
    `# Read Key Count: ${block.read_key_count}`,
    headers.join(','),
    ...rows.map((row) => row.join(',')),
  ].join('\r\n');

  return csvContent;
}

/**
 * Export storage reads to JSON format with metadata enrichment
 */
export function exportToJson(reads: StorageRead[], block: Block): string {
  const enrichedData = {
    block: {
      index: block.block_index,
      hash: block.hash,
      timestamp: new Date(block.timestamp_ms).toISOString(),
      timestampMs: block.timestamp_ms,
      transactionCount: block.tx_count,
      readKeyCount: block.read_key_count,
    },
    storageReads: reads.map((read) => ({
      readOrder: read.read_order,
      contract: {
        id: read.contract_id,
        hash: read.contracts?.contract_hash ?? null,
        name: read.contracts?.manifest_name ?? null,
      },
      key: {
        base64: read.key_base64,
        hex: base64ToHex(read.key_base64),
      },
      value: {
        base64: read.value_base64,
        hex: base64ToHex(read.value_base64),
      },
      txHash: read.tx_hash,
      source: read.source,
    })),
    exportedAt: new Date().toISOString(),
  };

  return JSON.stringify(enrichedData, null, 2);
}

/**
 * Convert base64 string to hexadecimal
 */
export function base64ToHex(base64: string): string {
  try {
    const bytes = atob(base64);
    let hex = '';
    for (let i = 0; i < bytes.length; i++) {
      hex += bytes.charCodeAt(i).toString(16).padStart(2, '0');
    }
    return hex;
  } catch {
    return '(invalid base64)';
  }
}

/**
 * Trigger file download in browser
 */
export function downloadFile(content: string | Blob, filename: string, mimeType: string): void {
  const blob = content instanceof Blob ? content : new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Format timestamp for display
 */
export function formatTimestamp(timestampMs: number): string {
  return new Date(timestampMs).toLocaleString();
}

/**
 * Truncate string with ellipsis
 */
export function truncate(str: string, maxLength: number): string {
  if (str.length <= maxLength) return str;
  return `${str.slice(0, maxLength - 3)}...`;
}
