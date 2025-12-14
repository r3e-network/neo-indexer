import { useCallback, useEffect, useMemo, useState } from 'react';
import type { Block, ExportFormat, StorageRead } from '../../types';
import { downloadStateFile, getAllStorageReads, getStorageReads, stateFileExists } from '../../services/supabase';
import { downloadFile, exportToCsv, exportToJson } from '../../utils/export';

type DisplayMode = 'base64' | 'hex';

export function useStorageReadsTable({ block, pageSize }: { block: Block; pageSize: number }) {
  const [reads, setReads] = useState<StorageRead[]>([]);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [hasBinary, setHasBinary] = useState(false);
  const [hasCsv, setHasCsv] = useState(false);
  const [hasJson, setHasJson] = useState(false);
  const [displayMode, setDisplayMode] = useState<DisplayMode>('hex');

  const fetchReads = useCallback(
    async (pageNum: number) => {
      setLoading(true);
      setError(null);
      try {
        const result = await getStorageReads(block.block_index, pageNum, pageSize);
        setReads(result.data);
        setTotalCount(result.count);
        setPage(pageNum);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load storage reads');
      } finally {
        setLoading(false);
      }
    },
    [block.block_index, pageSize]
  );

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

        const shouldUseStorage = format === 'csv' ? hasCsv : hasJson;
        if (shouldUseStorage) {
          const blob = await downloadStateFile(block.block_index, format === 'csv' ? 'csv' : 'json');
          downloadFile(blob, `block-${block.block_index}.${format}`, format === 'csv' ? 'text/csv' : 'application/json');
          return;
        }

        const allReads = await getAllStorageReads(block.block_index);
        if (format === 'csv') {
          const csv = exportToCsv(allReads, block);
          downloadFile(csv, `block-${block.block_index}.csv`, 'text/csv');
          return;
        }

        const json = exportToJson(allReads, block);
        downloadFile(json, `block-${block.block_index}.json`, 'application/json');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Export failed');
      } finally {
        setExporting(false);
      }
    },
    [block, hasBinary, hasCsv, hasJson]
  );

  const totalPages = useMemo(() => Math.ceil(totalCount / pageSize), [pageSize, totalCount]);

  return {
    reads,
    loading,
    exporting,
    error,
    page,
    totalCount,
    totalPages,
    hasBinary,
    hasCsv,
    hasJson,
    displayMode,
    setDisplayMode,
    fetchReads,
    handleExport,
  };
}

export type StorageReadsTableModel = ReturnType<typeof useStorageReadsTable>;

