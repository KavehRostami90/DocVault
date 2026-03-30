import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, FileText, Tag, Trash2, Plus, X, Save, AlertTriangle } from 'lucide-react'
import { getDocument, updateTags, deleteDocument } from '../api/documents'
import StatusBadge from '../components/StatusBadge'
import type { DocumentDetail } from '../types'

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export default function DocumentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [doc, setDoc] = useState<DocumentDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [tags, setTags] = useState<string[]>([])
  const [tagInput, setTagInput] = useState('')
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [tagsDirty, setTagsDirty] = useState(false)

  useEffect(() => {
    if (!id) return
    getDocument(id).then(d => { setDoc(d); setTags(d.tags.slice()) }).finally(() => setLoading(false))
  }, [id])

  const addTag = () => {
    const t = tagInput.trim().toLowerCase()
    if (t && !tags.includes(t)) { setTags(prev => [...prev, t]); setTagsDirty(true) }
    setTagInput('')
  }

  const removeTag = (t: string) => { setTags(prev => prev.filter(x => x !== t)); setTagsDirty(true) }

  const saveTags = async () => {
    if (!id) return
    setSaving(true)
    try { await updateTags(id, tags); setTagsDirty(false) } finally { setSaving(false) }
  }

  const handleDelete = async () => {
    if (!id) return
    setDeleting(true)
    try { await deleteDocument(id); navigate('/documents', { replace: true }) } finally { setDeleting(false) }
  }

  if (loading) return (
    <div className="max-w-2xl mx-auto animate-pulse space-y-4">
      <div className="h-4 bg-slate-800 rounded w-32" />
      <div className="bg-slate-900 rounded-2xl border border-slate-800 p-6 space-y-4">
        <div className="h-6 bg-slate-800 rounded w-1/2" />
        <div className="grid grid-cols-2 gap-4"><div className="h-16 bg-slate-800 rounded-lg" /><div className="h-16 bg-slate-800 rounded-lg" /></div>
      </div>
    </div>
  )

  if (!doc) return (
    <div className="max-w-2xl mx-auto text-center py-24">
      <p className="text-slate-400">Document not found.</p>
      <button onClick={() => navigate('/documents')} className="mt-4 text-indigo-400 hover:text-indigo-300 text-sm">← Back</button>
    </div>
  )

  return (
    <div className="max-w-2xl mx-auto">
      <button onClick={() => navigate('/documents')} className="flex items-center gap-2 text-slate-400 hover:text-white text-sm mb-6 transition-colors">
        <ArrowLeft className="w-4 h-4" />Back to Documents
      </button>

      <div className="bg-slate-900 rounded-2xl border border-slate-800 p-6 mb-4">
        <div className="flex items-start gap-4 mb-6">
          <div className="w-12 h-12 bg-indigo-600/10 rounded-xl flex items-center justify-center shrink-0">
            <FileText className="w-6 h-6 text-indigo-400" />
          </div>
          <div className="flex-1 min-w-0">
            <h1 className="text-xl font-bold text-white truncate">{doc.title}</h1>
            <p className="text-slate-500 text-sm mt-0.5">{doc.fileName}</p>
          </div>
          <StatusBadge status={doc.status} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          {[['Content Type', doc.contentType], ['File Size', formatBytes(doc.size)], ['Status', doc.status], ['ID', doc.id.slice(0, 8) + '...']].map(([label, value]) => (
            <div key={label} className="bg-slate-800/50 rounded-lg p-3">
              <p className="text-slate-500 text-xs mb-1">{label}</p>
              <p className="text-white text-sm font-medium truncate">{value}</p>
            </div>
          ))}
        </div>
      </div>

      <div className="bg-slate-900 rounded-2xl border border-slate-800 p-6 mb-4">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <Tag className="w-4 h-4 text-slate-400" />
            <h2 className="text-white font-medium">Tags</h2>
          </div>
          {tagsDirty && (
            <button onClick={saveTags} disabled={saving} className="flex items-center gap-1.5 text-xs bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white px-3 py-1.5 rounded-lg transition-colors">
              <Save className="w-3 h-3" />{saving ? 'Saving...' : 'Save'}
            </button>
          )}
        </div>
        <div className="flex gap-2 mb-3">
          <div className="flex-1 flex items-center gap-2 bg-slate-800 border border-slate-700 rounded-lg px-3 py-2">
            <input value={tagInput} onChange={e => setTagInput(e.target.value)} onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addTag() } }} placeholder="Add a tag..." className="flex-1 bg-transparent text-sm text-white placeholder-slate-500 focus:outline-none" />
          </div>
          <button onClick={addTag} className="px-3 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors"><Plus className="w-4 h-4" /></button>
        </div>
        {tags.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            {tags.map(t => (
              <span key={t} className="inline-flex items-center gap-1.5 bg-indigo-600/15 text-indigo-400 text-xs px-2.5 py-1 rounded-full border border-indigo-600/20">
                {t}<button onClick={() => removeTag(t)} className="hover:text-white transition-colors"><X className="w-3 h-3" /></button>
              </span>
            ))}
          </div>
        ) : <p className="text-slate-600 text-sm">No tags — add some above</p>}
      </div>

      <div className="bg-slate-900 rounded-2xl border border-red-900/30 p-6">
        <div className="flex items-center gap-2 mb-4">
          <AlertTriangle className="w-4 h-4 text-red-400" />
          <h2 className="text-white font-medium">Danger Zone</h2>
        </div>
        {!confirmDelete ? (
          <div className="flex items-center justify-between">
            <div>
              <p className="text-slate-300 text-sm font-medium">Delete this document</p>
              <p className="text-slate-500 text-xs mt-0.5">This action cannot be undone</p>
            </div>
            <button onClick={() => setConfirmDelete(true)} className="flex items-center gap-2 bg-red-600/10 hover:bg-red-600/20 text-red-400 border border-red-600/20 px-4 py-2 rounded-lg text-sm transition-colors">
              <Trash2 className="w-4 h-4" />Delete
            </button>
          </div>
        ) : (
          <div className="bg-red-900/10 border border-red-900/30 rounded-lg p-4">
            <p className="text-white text-sm font-medium mb-3">Are you sure? This cannot be undone.</p>
            <div className="flex gap-3">
              <button onClick={handleDelete} disabled={deleting} className="bg-red-600 hover:bg-red-500 disabled:opacity-50 text-white text-sm px-4 py-2 rounded-lg transition-colors">{deleting ? 'Deleting...' : 'Yes, delete'}</button>
              <button onClick={() => setConfirmDelete(false)} className="text-slate-400 hover:text-white text-sm px-4 py-2 transition-colors">Cancel</button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
