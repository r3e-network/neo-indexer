import { BlockSearch } from '../components/BlockSearch';
import { StorageReadsTable } from '../components/StorageReadsTable';
import { BlockResults } from './blockState/BlockResults';
import { BlockStateHeader } from './blockState/BlockStateHeader';
import { useBlockStatePage } from './blockState/useBlockStatePage';

export function BlockStatePage() {
  const model = useBlockStatePage();

  return (
    <div className="app">
      <BlockStateHeader />

      <main className="app-main">
        <section className="search-section">
          <BlockSearch onSearch={model.handleSearch} loading={model.loading} />
        </section>

        {model.error && (
          <div className="error-banner" role="alert">
            <span>{model.error}</span>
            <button onClick={() => model.handleSearch(model.currentFilters)} className="btn-retry">
              Retry
            </button>
          </div>
        )}

        {model.blocks && (
          <BlockResults
            blocks={model.blocks}
            selectedBlock={model.selectedBlock}
            page={model.page}
            loading={model.loading}
            onBlockSelect={model.handleBlockSelect}
            onPageChange={model.handlePageChange}
          />
        )}

        {model.selectedBlock && (
          <section className="details-section">
            <StorageReadsTable block={model.selectedBlock} />
          </section>
        )}
      </main>

      <footer className="app-footer">
        <p>Neo Block State Indexer &copy; 2025</p>
      </footer>
    </div>
  );
}
