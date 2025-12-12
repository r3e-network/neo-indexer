import { useState, useCallback } from 'react';
import type { BlockSearchFilters } from '../types';

interface BlockSearchProps {
  onSearch: (filters: BlockSearchFilters) => void;
  loading?: boolean;
}

export function BlockSearch({ onSearch, loading = false }: BlockSearchProps) {
  const [blockIndex, setBlockIndex] = useState('');
  const [hash, setHash] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');

  const handleSubmit = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      const filters: BlockSearchFilters = {};

      if (blockIndex.trim()) {
        const parsed = parseInt(blockIndex, 10);
        if (!isNaN(parsed) && parsed >= 0) {
          filters.blockIndex = parsed;
        }
      }
      if (hash.trim()) {
        filters.hash = hash.trim();
      }
      if (startDate) {
        filters.startDate = new Date(startDate);
      }
      if (endDate) {
        filters.endDate = new Date(endDate);
      }

      onSearch(filters);
    },
    [blockIndex, hash, startDate, endDate, onSearch]
  );

  const handleClear = useCallback(() => {
    setBlockIndex('');
    setHash('');
    setStartDate('');
    setEndDate('');
    onSearch({});
  }, [onSearch]);

  return (
    <form className="block-search" onSubmit={handleSubmit} data-testid="block-search-form">
      <div className="search-row">
        <div className="search-field">
          <label htmlFor="blockIndex">Block Index</label>
          <input
            id="blockIndex"
            type="number"
            min="0"
            value={blockIndex}
            onChange={(e) => setBlockIndex(e.target.value)}
            placeholder="Enter block index"
            disabled={loading}
          />
        </div>
        <div className="search-field">
          <label htmlFor="hash">Block Hash</label>
          <input
            id="hash"
            type="text"
            value={hash}
            onChange={(e) => setHash(e.target.value)}
            placeholder="0x..."
            disabled={loading}
          />
        </div>
      </div>
      <div className="search-row">
        <div className="search-field">
          <label htmlFor="startDate">Start Date</label>
          <input
            id="startDate"
            type="datetime-local"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
            disabled={loading}
          />
        </div>
        <div className="search-field">
          <label htmlFor="endDate">End Date</label>
          <input
            id="endDate"
            type="datetime-local"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
            disabled={loading}
          />
        </div>
      </div>
      <div className="search-actions">
        <button type="submit" disabled={loading} className="btn-primary">
          {loading ? 'Searching...' : 'Search'}
        </button>
        <button type="button" onClick={handleClear} disabled={loading} className="btn-secondary">
          Clear
        </button>
      </div>
    </form>
  );
}
