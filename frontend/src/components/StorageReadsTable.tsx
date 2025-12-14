import type { Block } from '../types';
import { base64ToHex, truncate } from '../utils/export';
import { StorageReadsHeader } from './storageReads/StorageReadsHeader';
import { StorageReadsPagination } from './storageReads/StorageReadsPagination';
import { useStorageReadsTable } from './storageReads/useStorageReadsTable';

interface StorageReadsTableProps {
  block: Block;
}

const PAGE_SIZE = 50;

export function StorageReadsTable({ block }: StorageReadsTableProps) {
  const model = useStorageReadsTable({ block, pageSize: PAGE_SIZE });

  const formatValue = (base64: string) => {
    if (model.displayMode === 'base64') {
      return truncate(base64, 40);
    }
    return truncate(base64ToHex(base64), 40);
  };

  return (
    <div className="storage-reads-table" data-testid="storage-reads-table">
      <StorageReadsHeader
        block={block}
        displayMode={model.displayMode}
        onDisplayModeChange={model.setDisplayMode}
        onExport={model.handleExport}
        exporting={model.exporting}
        hasCsv={model.hasCsv}
        hasJson={model.hasJson}
        hasBinary={model.hasBinary}
      />

      {model.error && (
        <div className="error-message" role="alert">
          {model.error}
          <button onClick={() => model.fetchReads(model.page)} className="btn-retry">
            Retry
          </button>
        </div>
      )}

      {model.loading ? (
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
              {model.reads.length === 0 ? (
                <tr>
                  <td colSpan={6} className="empty-state">
                    No storage reads found
                  </td>
                </tr>
              ) : (
                model.reads.map((read) => (
                  <tr key={read.id}>
                    <td>{read.read_order}</td>
                    <td title={read.contracts?.contract_hash ?? 'N/A'}>
                      {read.contracts?.manifest_name ?? truncate(read.contracts?.contract_hash ?? 'N/A', 12)}
                    </td>
                    <td
                      className="monospace"
                      title={model.displayMode === 'hex' ? base64ToHex(read.key_base64) : read.key_base64}
                    >
                      {formatValue(read.key_base64)}
                    </td>
                    <td
                      className="monospace"
                      title={model.displayMode === 'hex' ? base64ToHex(read.value_base64) : read.value_base64}
                    >
                      {formatValue(read.value_base64)}
                    </td>
                    <td title={read.tx_hash ?? undefined}>{read.tx_hash ? truncate(read.tx_hash, 12) : '-'}</td>
                    <td>{read.source ?? '-'}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>

          <StorageReadsPagination
            page={model.page}
            totalPages={model.totalPages}
            totalCount={model.totalCount}
            loading={model.loading}
            onPageChange={model.fetchReads}
          />
        </>
      )}
    </div>
  );
}
