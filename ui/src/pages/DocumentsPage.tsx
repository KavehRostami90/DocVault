import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, Upload, FileText, ChevronLeft, ChevronRight, X } from 'lucide-react'
import { listDocuments } from '../api/documents'
import { listTags } from '../api/tags'
import StatusBadge from '../components/StatusBadge'
import UploadModal from '../components/UploadModal'
import type { DocumentListItem, PageResponse } from '../types'

const STATUSES = ['Pending', 'Imported', 'Indexed', 'Failed']
const PAGE_SIZE = 12

export default function DocumentsPage() {
  const navigate = useNavigate()
  const [data, setData] = useState<PageResponse<DocumentListItem> | null>(null)
  const [tags, setTags] = useState<string[]>([])
  const [page, setPage] = useState(1)
  const [titleFilter, setTitleFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [tagFilter, setTagFilter] = useState('')
  const [showUpload, setShowUpload] = useState(false)
  const [loading, setLoading] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const result = await listDocuments({ page, size: PAGE_SIZE, title: titleFilter || undefined, status: statusFilter || undefined, tag: tagFilter || undefined })
      setData(result)
    } catch { /* ignore */ } finally {
      setLoading(false)
    }
  }, [page, titleFilter, statusFilter, tagFilter])

  useEffect(() => { load() }, [load])
  useEffect(() => { listTags().then(ts => setTags(ts.map(t => t.name))) }, [])

  const totalPages = data ? Math.ceil(data.totalCount / PAGE_SIZE) : 1
  const hasFilters = titleFilter || statusFilter || tagFilter

  return (
    <div className="max-w-7xl mx-auto">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-2xl font-bold text-white">Documents</h1>
          <p className="text-slate-400 text-sm mt-1">{data ? `${data.totalCount} document${data.totalCount !== 1 ? 's' : ''}` : 'Loading...'}</p>
        </div>
        <button onClick={() => setShowUpload(true)} className="flex items-center gap-2 bg-indigo-600 hover:bg-indigo-500 text-white px-4 py-2.5 rounded-lg text-sm font-medium transition-colors">
          <Upload className="w-4 h-4" />Upload
        </button>
      </div>

      <div className="flex flex-wrap items-center gap-3 mb-6">
        <div className="flex-1 min-w-48 relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
          <input value={titleFilter} onChange={e => { setTitleFilter(e.target.value); setPage(1) }} placeholder="Filter by title..." className="w-full bg-slate-900 border border-slate-700 rounded-lg pl-9 pr-3 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500" />
        </div>
        <select value={statusFilter} onChange={e => { setStatusFilter(e.target.value); setPage(1) }} className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2.5 text-sm text-white focus:outline-none focus:border-indigo-500">
          <option value="">All statuses</option>
          {STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
        </select>
        <select value={tagFilter} onChange={e => { setTagFilter(e.target.value); setPage(1) }} className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2.5 text-sm text-white focus:outline-none focus:border-indigo-500">
          <option value="">All tags</option>
          {tags.map(t => <option key={t} value={t}>{t}</option>)}
        </select>
        {hasFilters && (
          <button onClick={() => { setTitleFilter(''); setStatusFilter(''); setTagFilter(''); setPage(1) }} className="flex items-center gap-1.5 text-sm text-slate-400 hover:text-white transition-colors">
            <X className="w-4 h-4" />Clear
          </button>
        )}
      </div>

      {loading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="bg-slate-900 rounded-xl border border-slate-800 p-5 animate-pulse">
              <div className="h-4 bg-slate-800 rounded w-3/4 mb-3" />
              <div className="h-3 bg-slate-800 rounded w-1/2 mb-4" />
              <div className="h-5 bg-slate-800 rounded w-20" />
            </div>
          ))}
        </div>
      ) : data?.items.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <div className="w-16 h-16 bg-slate-800 rounded-2xl flex items-center justify-center mb-4">
            <FileText className="w-8 h-8 text-slate-600" />
          </div>
          <p className="text-slate-400 font-medium">No documents found</p>
          <p className="text-slate-600 text-sm mt-1">{hasFilters ? 'Try adjusting your filters' : 'Upload your first document to get started'}</p>
          {!hasFilters && <button onClick={() => setShowUpload(true)} className="mt-4 text-indigo-400 hover:text-indigo-300 text-sm font-medium transition-colors">Upload a document →</button>}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {data?.items.map(doc => (
            <button key={doc.id} onClick={() => navigate(`/documents/${doc.id}`)} className="bg-slate-900 hover:bg-slate-800 border border-slate-800 hover:border-slate-700 rounded-xl p-5 text-left transition-all group">
              <div className="flex items-start gap-3 mb-3">
                <div className="w-9 h-9 bg-indigo-600/10 rounded-lg flex items-center justify-center shrink-0 group-hover:bg-indigo-600/20 transition-colors">
                  <FileText className="w-4 h-4 text-indigo-400" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-white font-medium text-sm truncate">{doc.title}</p>
                  <p className="text-slate-500 text-xs truncate mt-0.5">{doc.fileName}</p>
                </div>
              </div>
              <StatusBadge status={doc.status} />
            </button>
          ))}
        </div>
      )}

      {totalPages > 1 && (
        <div className="flex items-center justify-between mt-8">
          <p className="text-slate-500 text-sm">Page {page} of {totalPages}</p>
          <div className="flex items-center gap-2">
            <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} className="p-2 rounded-lg bg-slate-900 border border-slate-800 text-slate-400 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-colors"><ChevronLeft className="w-4 h-4" /></button>
            <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages} className="p-2 rounded-lg bg-slate-900 border border-slate-800 text-slate-400 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-colors"><ChevronRight className="w-4 h-4" /></button>
          </div>
        </div>
      )}

      {showUpload && <UploadModal onClose={() => setShowUpload(false)} onUploaded={id => { setShowUpload(false); navigate(`/documents/${id}`) }} />}
    </div>
  )
}
