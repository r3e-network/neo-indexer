import type { Block, PaginatedResponse } from '../../types';

export function BlockResults({
  blocks,
  selectedBlock,
  page,
  loading,
  onBlockSelect,
  onPageChange,
}: {
  blocks: PaginatedResponse<Block>;
  selectedBlock: Block | null;
  page: number;
  loading: boolean;
  onBlockSelect: (block: Block) => void;
  onPageChange: (page: number) => void;
}) {
  return (
    <section className="results-section">
      <h2>Search Results ({blocks.count} blocks found)</h2>
      {blocks.data.length > 0 ? (
        <>
          <div className="block-list">
            {blocks.data.map((block) => (
              <div
                key={block.block_index}
                className={`block-card ${selectedBlock?.block_index === block.block_index ? 'selected' : ''}`}
                onClick={() => onBlockSelect(block)}
                role="button"
                tabIndex={0}
                onKeyDown={(event) => event.key === 'Enter' && onBlockSelect(block)}
              >
                <div className="block-index">#{block.block_index}</div>
                <div className="block-hash" title={block.hash}>
                  {block.hash.slice(0, 16)}...
                </div>
                <div className="block-meta">
                  <span>{block.tx_count} txs</span>
                  <span>{block.read_key_count} reads</span>
                </div>
              </div>
            ))}
          </div>

          {blocks.count > blocks.pageSize && (
            <div className="pagination">
              <button onClick={() => onPageChange(1)} disabled={page === 1 || loading}>
                First
              </button>
              <button onClick={() => onPageChange(page - 1)} disabled={page === 1 || loading}>
                Previous
              </button>
              <span>
                Page {page} of {Math.ceil(blocks.count / blocks.pageSize)}
              </span>
              <button onClick={() => onPageChange(page + 1)} disabled={!blocks.hasMore || loading}>
                Next
              </button>
            </div>
          )}
        </>
      ) : (
        <p className="no-results">No blocks found matching your search criteria.</p>
      )}
    </section>
  );
}

