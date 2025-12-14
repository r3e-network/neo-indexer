import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { BlockStatePage } from './pages/BlockStatePage';
import TraceBrowser from './pages/TraceBrowser';
import './App.css';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<BlockStatePage />} />
        <Route path="/traces" element={<TraceBrowser />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
