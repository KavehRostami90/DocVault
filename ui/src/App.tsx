import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import Layout from './components/Layout'
import DocumentsPage from './pages/DocumentsPage'
import DocumentDetailPage from './pages/DocumentDetailPage'
import SearchPage from './pages/SearchPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<Navigate to="/documents" replace />} />
          <Route path="documents" element={<DocumentsPage />} />
          <Route path="documents/:id" element={<DocumentDetailPage />} />
          <Route path="search" element={<SearchPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
