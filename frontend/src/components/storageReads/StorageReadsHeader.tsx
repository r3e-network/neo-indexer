import type { Block, ExportFormat } from '../../types';
import { formatTimestamp, truncate } from '../../utils/export';

export function StorageReadsHeader({
  block,
  displayMode,
  onDisplayModeChange,
  onExport,
  exporting,
  hasCsv,
  hasJson,
  hasBinary,
}: {
  block: Block;
  displayMode: 'base64' | 'hex';
  onDisplayModeChange: (mode: 'base64' | 'hex') => void;
  onExport: (format: ExportFormat) => void;
  exporting: boolean;
  hasCsv: boolean;
  hasJson: boolean;
  hasBinary: boolean;
}) {
  return (
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
          onChange={(event) => onDisplayModeChange(event.target.value as 'base64' | 'hex')}
          className="display-mode"
        >
          <option value="hex">Hex</option>
          <option value="base64">Base64</option>
        </select>
        <button onClick={() => onExport('csv')} disabled={exporting} className="btn-export">
          {hasCsv ? 'Download CSV' : 'Export CSV'}
        </button>
        <button onClick={() => onExport('json')} disabled={exporting} className="btn-export">
          {hasJson ? 'Download JSON' : 'Export JSON'}
        </button>
        {hasBinary && (
          <button onClick={() => onExport('binary')} disabled={exporting} className="btn-export btn-binary">
            Download Binary
          </button>
        )}
      </div>
    </div>
  );
}

