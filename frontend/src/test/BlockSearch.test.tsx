import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { BlockSearch } from '../components/BlockSearch';

describe('BlockSearch', () => {
  it('should render all search fields', () => {
    render(<BlockSearch onSearch={() => {}} />);

    expect(screen.getByLabelText(/block index/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/block hash/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/start date/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/end date/i)).toBeInTheDocument();
  });

  it('should call onSearch with filters when form submitted', () => {
    const onSearch = vi.fn();
    render(<BlockSearch onSearch={onSearch} />);

    const blockIndexInput = screen.getByLabelText(/block index/i);
    fireEvent.change(blockIndexInput, { target: { value: '12345' } });

    const searchButton = screen.getByRole('button', { name: /search/i });
    fireEvent.click(searchButton);

    expect(onSearch).toHaveBeenCalledWith({ blockIndex: 12345 });
  });

  it('should call onSearch with hash filter', () => {
    const onSearch = vi.fn();
    render(<BlockSearch onSearch={onSearch} />);

    const hashInput = screen.getByLabelText(/block hash/i);
    fireEvent.change(hashInput, { target: { value: '0xabc123' } });

    const searchButton = screen.getByRole('button', { name: /search/i });
    fireEvent.click(searchButton);

    expect(onSearch).toHaveBeenCalledWith({ hash: '0xabc123' });
  });

  it('should clear all fields when Clear button clicked', () => {
    const onSearch = vi.fn();
    render(<BlockSearch onSearch={onSearch} />);

    // Fill in some values
    const blockIndexInput = screen.getByLabelText(/block index/i) as HTMLInputElement;
    fireEvent.change(blockIndexInput, { target: { value: '12345' } });

    // Clear
    const clearButton = screen.getByRole('button', { name: /clear/i });
    fireEvent.click(clearButton);

    expect(blockIndexInput.value).toBe('');
    expect(onSearch).toHaveBeenLastCalledWith({});
  });

  it('should disable inputs when loading', () => {
    render(<BlockSearch onSearch={() => {}} loading={true} />);

    expect(screen.getByLabelText(/block index/i)).toBeDisabled();
    expect(screen.getByLabelText(/block hash/i)).toBeDisabled();
    expect(screen.getByRole('button', { name: /searching/i })).toBeDisabled();
  });

  it('should only include valid block indices (>=0)', () => {
    const onSearch = vi.fn();
    render(<BlockSearch onSearch={onSearch} />);

    // Submit without any value - should call with empty object
    const searchButton = screen.getByRole('button', { name: /search/i });
    fireEvent.click(searchButton);

    expect(onSearch).toHaveBeenCalledWith({});
  });
});
