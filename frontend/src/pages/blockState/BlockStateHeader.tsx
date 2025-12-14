import { Link } from 'react-router-dom';

export function BlockStateHeader() {
  return (
    <header className="app-header">
      <h1>Neo Block State Viewer</h1>
      <p>Query and download storage state reads from executed blocks</p>
      <div className="header-actions">
        <Link to="/traces" className="btn-secondary">
          Open Trace Browser
        </Link>
      </div>
    </header>
  );
}

