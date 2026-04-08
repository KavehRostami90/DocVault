import { useEffect, useState } from 'react'
import { RefreshCw, Trash2 } from 'lucide-react'
import { adminApi, type AdminDocument } from '../../api/admin'
import ConfirmDialog from '../../components/ConfirmDialog'
import StatusBadge from '../../components/StatusBadge'
import type { DocumentStatus } from '../../types'

export default function AdminDocumentsTab() {
  const [docs, setDocs] = useState<AdminDocument[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [pendingDelete, setPendingDelete] = useState<AdminDocument | null>(null)
  const [toastMsg, setToastMsg] = useState('')

  const PAGE_SIZE = 20

  useEffect(() => {
    load(page)
  }, [page])

  function load(p: number) {
    setLoading(true)
    adminApi.listDocuments(p, PAGE_SIZE)
      .then(r => {
        setDocs(r.items)
        setTotalCount(r.totalCount)
      })
      .catch(() => setError('Failed to load documents.'))
      .finally(() => setLoading(false))
  }

  function toast(msg: string) {
    setToastMsg(msg)
    setTimeout(() => setToastMsg(''), 3000)
  }

  async function handleDeleteConfirm() {
    if (!pendingDelete) return
    try {
      await adminApi.deleteDocument(pendingDelete.id)
      setDocs(d => d.filter(x => x.id !== pendingDelete.id))
      setTotalCount(c => c - 1)
      toast(`Document "${pendingDelete.title}" deleted.`)
    } catch {
      toast('Failed to delete document.')
    } finally {
      setPendingDelete(null)
    }
  }

  async function handleReindex(doc: AdminDocument) {
    try {
      await adminApi.reindexDocument(doc.id)
      setDocs(prev => prev.map(d => d.id === doc.id ? { ...d, status: 'Imported' } : d))
      toast(`"${doc.title}" queued for re-indexing.`)
    } catch {
      toast('Failed to queue document for re-indexing.')
    }
  }

  const formatSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  }

  const totalPages = Math.ceil(totalCount / PAGE_SIZE)

  return (
    <div className="space-y-4">
      {toastMsg && (
        <div className="bg-indigo-500/10 border border-indigo-500/30 text-indigo-300 text-sm rounded-lg px-4 py-3">
          {toastMsg}
        </div>
      )}

      {error && (
        <div className="bg-red-500/10 border border-red-500/30 text-red-400 text-sm rounded-lg px-4 py-3">
          {error}
        </div>
      )}

      <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-800 text-slate-400 text-left">
              <th className="px-4 py-3 font-medium">Title</th>
              <th className="px-4 py-3 font-medium">Status</th>
              <th className="px-4 py-3 font-medium">Size</th>
              <th className="px-4 py-3 font-medium">Created</th>
              <th className="px-4 py-3 font-medium">Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={5} className="px-4 py-8 text-center text-slate-500">Loading…</td></tr>
            ) : docs.map(d => (
              <tr key={d.id} className="border-b border-slate-800/50 hover:bg-slate-800/30">
                <td className="px-4 py-3 text-white font-medium max-w-xs truncate">{d.title}</td>
                <td className="px-4 py-3">
                  <StatusBadge status={d.status as DocumentStatus} />
                </td>
                <td className="px-4 py-3 text-slate-400">{formatSize(d.size)}</td>
                <td className="px-4 py-3 text-slate-400">{new Date(d.createdAt).toLocaleDateString()}</td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => handleReindex(d)}
                      className="p-1.5 rounded text-slate-500 hover:text-indigo-400 hover:bg-indigo-500/10 transition-colors"
                      title="Re-index document"
                    >
                      <RefreshCw className="w-4 h-4" />
                    </button>
                    <button
                      onClick={() => setPendingDelete(d)}
                      className="p-1.5 rounded text-slate-500 hover:text-red-400 hover:bg-red-500/10 transition-colors"
                      title="Delete document"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-slate-400">
          <span>{totalCount} documents</span>
          <div className="flex gap-2">
            <button
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-3 py-1 rounded bg-slate-800 hover:bg-slate-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              Previous
            </button>
            <span className="px-3 py-1">Page {page} / {totalPages}</span>
            <button
              onClick={() => setPage(p => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
              className="px-3 py-1 rounded bg-slate-800 hover:bg-slate-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              Next
            </button>
          </div>
        </div>
      )}

      {pendingDelete && (
        <ConfirmDialog
          title="Delete document"
          message={`Are you sure you want to delete "${pendingDelete.title}"? This action cannot be undone.`}
          confirmLabel="Delete"
          onConfirm={handleDeleteConfirm}
          onCancel={() => setPendingDelete(null)}
        />
      )}
    </div>
  )
}
