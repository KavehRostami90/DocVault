import { useEffect, useRef, useState } from 'react'
import { Download, Eye, RefreshCw, Trash2 } from 'lucide-react'
import { adminApi, type AdminDocument } from '../../api/admin'
import ConfirmDialog from '../../components/ConfirmDialog'
import StatusBadge from '../../components/StatusBadge'
import type { DocumentStatus } from '../../types'
import type { AdminDocumentFilter } from './adminFilters'

interface Props {
  filter?: AdminDocumentFilter
  onClearFilter?: () => void
}

export default function AdminDocumentsTab({ filter = 'all', onClearFilter }: Props) {
  const [docs, setDocs] = useState<AdminDocument[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [pendingDelete, setPendingDelete] = useState<AdminDocument | null>(null)
  const [toastMsg, setToastMsg] = useState('')
  const [previewingId, setPreviewingId] = useState<string | null>(null)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [confirmBulkDelete, setConfirmBulkDelete] = useState(false)
  const [bulkLoading, setBulkLoading] = useState(false)
  const allCheckboxRef = useRef<HTMLInputElement>(null)

  const PAGE_SIZE = 20
  const allSelected = docs.length > 0 && docs.every(d => selected.has(d.id))
  const someSelected = !allSelected && docs.some(d => selected.has(d.id))

  useEffect(() => {
    setPage(1)
    setSelected(new Set())
  }, [filter])

  useEffect(() => {
    setSelected(new Set())
    load(page)
  }, [page, filter])

  useEffect(() => {
    if (allCheckboxRef.current)
      allCheckboxRef.current.indeterminate = someSelected
  }, [someSelected])

  function load(p: number) {
    setLoading(true)
    setError('')
    adminApi.listDocuments(p, PAGE_SIZE, filter)
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

  function toggleSelect(id: string) {
    setSelected(prev => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  function toggleAll() {
    setSelected(allSelected ? new Set() : new Set(docs.map(d => d.id)))
  }

  async function handleDeleteConfirm() {
    if (!pendingDelete) return
    try {
      await adminApi.deleteDocument(pendingDelete.id)
      setDocs(d => d.filter(x => x.id !== pendingDelete.id))
      setTotalCount(c => c - 1)
      setSelected(prev => { const next = new Set(prev); next.delete(pendingDelete.id); return next })
      toast(`Document "${pendingDelete.title}" deleted.`)
    } catch {
      toast('Failed to delete document.')
    } finally {
      setPendingDelete(null)
    }
  }

  async function handleBulkDeleteConfirm() {
    setConfirmBulkDelete(false)
    setBulkLoading(true)
    const ids = [...selected]
    try {
      const result = await adminApi.bulkDeleteDocuments(ids)
      setDocs(d => d.filter(x => !ids.includes(x.id)))
      setTotalCount(c => c - result.succeeded)
      setSelected(new Set())
      toast(`Deleted ${result.succeeded} document${result.succeeded !== 1 ? 's' : ''}${result.failed ? `, ${result.failed} failed` : ''}.`)
    } catch {
      toast('Bulk delete failed.')
    } finally {
      setBulkLoading(false)
    }
  }

  async function handleReindex(doc: AdminDocument) {
    try {
      await adminApi.reindexDocument(doc.id)
      if (filter !== 'all' && filter !== 'Imported') {
        setDocs(prev => prev.filter(d => d.id !== doc.id))
        setTotalCount(c => Math.max(0, c - 1))
      } else {
        setDocs(prev => prev.map(d => d.id === doc.id ? { ...d, status: 'Imported' } : d))
      }
      toast(`"${doc.title}" queued for re-indexing.`)
    } catch {
      toast('Failed to queue document for re-indexing.')
    }
  }

  async function handleBulkReindex() {
    setBulkLoading(true)
    const ids = [...selected]
    try {
      const result = await adminApi.bulkReindexDocuments(ids)
      if (filter !== 'all' && filter !== 'Imported') {
        setDocs(prev => prev.filter(d => !ids.includes(d.id)))
        setTotalCount(c => Math.max(0, c - result.succeeded))
      } else {
        setDocs(prev => prev.map(d => ids.includes(d.id) ? { ...d, status: 'Imported' } : d))
      }
      setSelected(new Set())
      toast(`Queued ${result.succeeded} document${result.succeeded !== 1 ? 's' : ''} for re-indexing${result.failed ? `, ${result.failed} failed` : ''}.`)
    } catch {
      toast('Bulk re-index failed.')
    } finally {
      setBulkLoading(false)
    }
  }

  async function handlePreview(doc: AdminDocument) {
    if (previewingId === doc.id) return
    setPreviewingId(doc.id)
    try {
      const blob = await adminApi.getDocumentPreviewBlob(doc.id)
      const objectUrl = window.URL.createObjectURL(blob)
      const win = window.open(objectUrl, '_blank')
      if (!win) {
        window.URL.revokeObjectURL(objectUrl)
        toast('Preview blocked — allow popups for this site and try again.')
        return
      }
      window.setTimeout(() => window.URL.revokeObjectURL(objectUrl), 60_000)
    } catch {
      toast(`Preview failed for "${doc.title}".`)
    } finally {
      setPreviewingId(null)
    }
  }

  async function handleDownload(doc: AdminDocument) {
    if (downloadingId === doc.id) return
    setDownloadingId(doc.id)
    try {
      const blob = await adminApi.getDocumentDownloadBlob(doc.id)
      const objectUrl = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = objectUrl
      link.download = doc.fileName
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.setTimeout(() => window.URL.revokeObjectURL(objectUrl), 5_000)
    } catch {
      toast(`Download failed for "${doc.title}".`)
    } finally {
      setDownloadingId(null)
    }
  }

  const formatSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  }

  const totalPages = Math.ceil(totalCount / PAGE_SIZE)
  const filterLabel = documentFilterLabel(filter)

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

      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-white font-medium">{filterLabel}</h2>
          <p className="text-sm text-slate-500">{totalCount} document{totalCount !== 1 ? 's' : ''}</p>
        </div>
        {filter !== 'all' && onClearFilter && (
          <button
            type="button"
            onClick={onClearFilter}
            className="rounded-lg border border-slate-700 px-3 py-1.5 text-sm text-slate-300 transition-colors hover:border-slate-600 hover:text-white"
          >
            Show all documents
          </button>
        )}
      </div>

      {selected.size > 0 && (
        <div className="flex items-center gap-3 bg-slate-800/60 border border-slate-700 rounded-lg px-4 py-2.5">
          <span className="text-sm font-medium text-slate-300 flex-1">
            {selected.size} selected
          </span>
          <button
            type="button"
            onClick={() => setSelected(new Set())}
            className="text-xs text-slate-500 hover:text-slate-300 px-2 py-1 rounded hover:bg-slate-700 transition-colors"
          >
            Deselect all
          </button>
          <button
            type="button"
            disabled={bulkLoading}
            onClick={handleBulkReindex}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-medium text-indigo-300 bg-indigo-500/10 hover:bg-indigo-500/20 border border-indigo-500/20 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            Re-index
          </button>
          <button
            type="button"
            disabled={bulkLoading}
            onClick={() => setConfirmBulkDelete(true)}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-medium text-red-400 bg-red-500/10 hover:bg-red-500/20 border border-red-500/20 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            <Trash2 className="w-3.5 h-3.5" />
            Delete
          </button>
        </div>
      )}

      <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-800 text-slate-400 text-left">
              <th className="px-4 py-3 w-10">
                <input
                  ref={allCheckboxRef}
                  type="checkbox"
                  checked={allSelected}
                  onChange={toggleAll}
                  disabled={docs.length === 0}
                  className="w-4 h-4 rounded border-slate-600 bg-slate-800 accent-indigo-500 cursor-pointer disabled:cursor-not-allowed"
                  aria-label="Select all"
                />
              </th>
              <th className="px-4 py-3 font-medium">Title</th>
              <th className="px-4 py-3 font-medium">Status</th>
              <th className="px-4 py-3 font-medium">Size</th>
              <th className="px-4 py-3 font-medium">Created</th>
              <th className="px-4 py-3 font-medium">Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={6} className="px-4 py-8 text-center text-slate-500">Loading…</td></tr>
            ) : docs.length === 0 ? (
              <tr><td colSpan={6} className="px-4 py-8 text-center text-slate-500">No documents found for this category.</td></tr>
            ) : docs.map(d => (
              <tr
                key={d.id}
                className={`border-b border-slate-800/50 transition-colors ${selected.has(d.id) ? 'bg-indigo-500/5' : 'hover:bg-slate-800/30'}`}
              >
                <td className="px-4 py-3">
                  <input
                    type="checkbox"
                    checked={selected.has(d.id)}
                    onChange={() => toggleSelect(d.id)}
                    className="w-4 h-4 rounded border-slate-600 bg-slate-800 accent-indigo-500 cursor-pointer"
                    aria-label={`Select ${d.title}`}
                  />
                </td>
                <td className="px-4 py-3 text-white font-medium max-w-xs truncate">{d.title}</td>
                <td className="px-4 py-3">
                  <StatusBadge status={d.status as DocumentStatus} />
                </td>
                <td className="px-4 py-3 text-slate-400">{formatSize(d.size)}</td>
                <td className="px-4 py-3 text-slate-400">{new Date(d.createdAt).toLocaleDateString()}</td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => handlePreview(d)}
                      disabled={previewingId === d.id}
                      className="p-1.5 rounded text-slate-500 hover:text-sky-400 hover:bg-sky-500/10 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                      title="Preview document"
                    >
                      <Eye className="w-4 h-4" />
                    </button>
                    <button
                      onClick={() => handleDownload(d)}
                      disabled={downloadingId === d.id}
                      className="p-1.5 rounded text-slate-500 hover:text-emerald-400 hover:bg-emerald-500/10 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                      title="Download document"
                    >
                      <Download className="w-4 h-4" />
                    </button>
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

      {confirmBulkDelete && (
        <ConfirmDialog
          title={`Delete ${selected.size} document${selected.size !== 1 ? 's' : ''}`}
          message={`Are you sure you want to permanently delete ${selected.size} selected document${selected.size !== 1 ? 's' : ''}? This action cannot be undone.`}
          confirmLabel={`Delete ${selected.size}`}
          onConfirm={handleBulkDeleteConfirm}
          onCancel={() => setConfirmBulkDelete(false)}
        />
      )}
    </div>
  )
}

function documentFilterLabel(filter: AdminDocumentFilter): string {
  return filter === 'all' ? 'All Documents' : `${filter} Documents`
}
