import { useState, useEffect, useCallback } from 'react';
import type { StorageRead, Block, ExportFormat } from '../types';
import { getStorageReads, getAllStorageReads, downloadStateFile, stateFileExists } from '../services/supabase';
import { exportToCsv, exportToJson, downloadFile, base64ToHex, truncate, formatTimestamp } from '../utils/export';

interface StorageReadsTableProps {
  block: Block;
}

const PAGE_SIZE = 50;

export function StorageReadsTable({ block }: StorageReadsTableProps) {
  const [reads, setReads] = useState<StorageRead[]>([]);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [hasBinary, setHasBinary] = useState(false);
  const [hasCsv, setHasCsv] = useState(false);
  const [hasJson, setHasJson] = useState(false);
  const [displayMode, setDisplayMode] = useState<'base64' | 'hex'>('hex');

  const fetchReads = useCallback(async (pageNum: number) => {
    setLoading(true);
    setError(null);
    try {
      const result = await getStorageReads(block.block_index, pageNum, PAGE_SIZE);
      setReads(result.data);
      setTotalCount(result.count);
      setPage(pageNum);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load storage reads');
    } finally {
      setLoading(false);
    }
  }, [block.block_index]);

  useEffect(() => {
    fetchReads(1);
    stateFileExists(block.block_index, 'bin').then(setHasBinary).catch(() => setHasBinary(false));
    stateFileExists(block.block_index, 'csv').then(setHasCsv).catch(() => setHasCsv(false));
    stateFileExists(block.block_index, 'json').then(setHasJson).catch(() => setHasJson(false));
  }, [block.block_index, fetchReads]);

  const handleExport = useCallback(
    async (format: ExportFormat) => {
      setExporting(true);
      setError(null);
      try {
        if (format === 'binary') {
          if (hasBinary) {
            const blob = await downloadStateFile(block.block_index, 'bin');
            downloadFile(blob, `block-${block.block_index}.bin`, 'application/octet-stream');
            return;
          }
          throw new Error('Binary file not available in storage for this block.');
        }

        // Prefer pre-built artifacts; fallback to local export if missing
        const shouldUseStorage = format === 'csv' ? hasCsv : hasJson;
        if (shouldUseStorage) {
          const blob = await downloadStateFile(block.block_index, format === 'csv' ? 'csv' : 'json');
          downloadFile(
            blob,
            `block-${block.block_index}.${format}`,
            format === 'csv' ? 'text/csv' : 'application/json'
          );
          return;
        }

        const allReads = await getAllStorageReads(block.block_index);
        if (format === 'csv') {
          const csv = exportToCsv(allReads, block);
          downloadFile(csv, `block-${block.block_index}.csv`, 'text/csv');
        } else {
          const json = exportToJson(allReads, block);
          downloadFile(json, `block-${block.block_index}.json`, 'application/json');
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Export failed');
      } finally {
        setExporting(false);
      }
    },
    [block, hasBinary, hasCsv, hasJson]
  );

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  const formatValue = (base64: string) => {
    if (displayMode === 'base64') {
      return truncate(base64, 40);
    }
    return truncate(base64ToHex(base64), 40);
  };

  return (
    <div className="storage-reads-table" data-testid="storage-reads-table">
      <div className="table-header">
        <div className="block-info">
          <h3>Block #{block.block_index}</h3>
          <p className="hash" title={block.hash}>
            {truncate(block.hash, 20)}
          </p>
          <p className="meta">
            {formatTimestamp(block.timestamp_ms)} | {block.tx_count} txs | {block.read_key_count} reads
          </p>
        </div>
        <div className="table-actions">
          <select
            value={displayMode}
            onChange={(e) => setDisplayMode(e.target.value as 'base64' | 'hex')}
            className="display-mode"
          >
            <option value="hex">Hex</option>
            <option value="base64">Base64</option>
          </select>
          <button onClick={() => handleExport('csv')} disabled={exporting} className="btn-export">
            {hasCsv ? 'Download CSV' : 'Export CSV'}
          </button>
          <button onClick={() => handleExport('json')} disabled={exporting} className="btn-export">
            {hasJson ? 'Download JSON' : 'Export JSON'}
          </button>
          {hasBinary && (
            <button onClick={() => handleExport('binary')} disabled={exporting} className="btn-export btn-binary">
              Download Binary
            </button>
          )}
        </div>
      </div>

      {error && (
        <div className="error-message" role="alert">
          {error}
          <button onClick={() => fetchReads(page)} className="btn-retry">
            Retry
          </button>
        </div>
      )}

      {loading ? (
        <div className="loading" aria-busy="true">
          Loading storage reads...
        </div>
      ) : (
        <>
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Contract</th>
                <th>Key</th>
                <th>Value</th>
                <th>Tx Hash</th>
                <th>Source</th>
              </tr>
            </thead>
            <tbody>
              {reads.length === 0 ? (
                <tr>
                  <td colSpan={6} className="empty-state">
                    No storage reads found
                  </td>
                </tr>
              ) : (
                reads.map((read) => (
                  <tr key={read.id}>
                    <td>{read.read_order}</td>
                    <td title={read.contracts?.contract_hash ?? 'N/A'}>
                      {read.contracts?.manifest_name ?? truncate(read.contracts?.contract_hash ?? 'N/A', 12)}
                    </td>
                    <td className="monospace" title={displayMode === 'hex' ? base64ToHex(read.key_base64) : read.key_base64}>
                      {formatValue(read.key_base64)}
                    </td>
                    <td className="monospace" title={displayMode === 'hex' ? base64ToHex(read.value_base64) : read.value_base64}>
                      {formatValue(read.value_base64)}
                    </td>
                    <td title={read.tx_hash ?? undefined}>{read.tx_hash ? truncate(read.tx_hash, 12) : '-'}</td>
                    <td>{read.source ?? '-'}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>

          {totalPages > 1 && (
            <div className="pagination">
              <button onClick={() => fetchReads(1)} disabled={page === 1 || loading}>
                First
              </button>
              <button onClick={() => fetchReads(page - 1)} disabled={page === 1 || loading}>
                Previous
              </button>
              <span>
                Page {page} of {totalPages} ({totalCount} total)
              </span>
              <button onClick={() => fetchReads(page + 1)} disabled={page === totalPages || loading}>
                Next
              </button>
              <button onClick={() => fetchReads(totalPages)} disabled={page === totalPages || loading}>
                Last
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
