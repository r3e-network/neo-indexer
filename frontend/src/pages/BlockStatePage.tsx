import { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import { BlockSearch } from '../components/BlockSearch';
import { StorageReadsTable } from '../components/StorageReadsTable';
import { getBlockByIndex, searchBlocks } from '../services/supabase';
import type { Block, BlockSearchFilters, PaginatedResponse } from '../types';

export function BlockStatePage() {
  const [blocks, setBlocks] = useState<PaginatedResponse<Block> | null>(null);
  const [selectedBlock, setSelectedBlock] = useState<Block | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [currentFilters, setCurrentFilters] = useState<BlockSearchFilters>({});

  const handleSearch = useCallback(async (filters: BlockSearchFilters, pageNum = 1) => {
    setLoading(true);
    setError(null);
    setCurrentFilters(filters);
    setPage(pageNum);

    try {
      if (filters.blockIndex !== undefined && !filters.hash && !filters.startDate && !filters.endDate) {
        const block = await getBlockByIndex(filters.blockIndex);
        if (block) {
          setBlocks({ data: [block], count: 1, page: 1, pageSize: 50, hasMore: false });
          setSelectedBlock(block);
        } else {
          setBlocks({ data: [], count: 0, page: 1, pageSize: 50, hasMore: false });
          setSelectedBlock(null);
        }
        return;
      }

      const result = await searchBlocks(filters, pageNum);
      setBlocks(result);
      if (result.data.length === 1) {
        setSelectedBlock(result.data[0]);
      } else {
        setSelectedBlock(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Search failed');
      setBlocks(null);
    } finally {
      setLoading(false);
    }
  }, []);

  const handlePageChange = useCallback(
    (newPage: number) => {
      handleSearch(currentFilters, newPage);
    },
    [currentFilters, handleSearch]
  );

  const handleBlockSelect = useCallback((block: Block) => {
    setSelectedBlock(block);
  }, []);

  return (
    <div className="app">
      <header className="app-header">
        <h1>Neo Block State Viewer</h1>
        <p>Query and download storage state reads from executed blocks</p>
        <div className="header-actions">
          <Link to="/traces" className="btn-secondary">
            Open Trace Browser
          </Link>
        </div>
      </header>

      <main className="app-main">
        <section className="search-section">
          <BlockSearch onSearch={handleSearch} loading={loading} />
        </section>

        {error && (
          <div className="error-banner" role="alert">
            <span>{error}</span>
            <button onClick={() => handleSearch(currentFilters)} className="btn-retry">
              Retry
            </button>
          </div>
        )}

        {blocks && (
          <section className="results-section">
            <h2>Search Results ({blocks.count} blocks found)</h2>
            {blocks.data.length > 0 ? (
              <>
                <div className="block-list">
                  {blocks.data.map((block) => (
                    <div
                      key={block.block_index}
                      className={`block-card ${selectedBlock?.block_index === block.block_index ? 'selected' : ''}`}
                      onClick={() => handleBlockSelect(block)}
                      role="button"
                      tabIndex={0}
                      onKeyDown={(event) => event.key === 'Enter' && handleBlockSelect(block)}
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
                    <button onClick={() => handlePageChange(1)} disabled={page === 1 || loading}>
                      First
                    </button>
                    <button onClick={() => handlePageChange(page - 1)} disabled={page === 1 || loading}>
                      Previous
                    </button>
                    <span>
                      Page {page} of {Math.ceil(blocks.count / blocks.pageSize)}
                    </span>
                    <button onClick={() => handlePageChange(page + 1)} disabled={!blocks.hasMore || loading}>
                      Next
                    </button>
                  </div>
                )}
              </>
            ) : (
              <p className="no-results">No blocks found matching your search criteria.</p>
            )}
          </section>
        )}

        {selectedBlock && (
          <section className="details-section">
            <StorageReadsTable block={selectedBlock} />
          </section>
        )}
      </main>

      <footer className="app-footer">
        <p>Neo Block State Indexer &copy; 2025</p>
      </footer>
    </div>
  );
}

