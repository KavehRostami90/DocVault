import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, Download, Eye, FileText, Tag, Trash2, Plus, X, Save, AlertTriangle, Copy, Check, Sparkles } from 'lucide-react'
import { getDocument, getDocumentDownloadBlob, getDocumentPreviewBlob, updateTags, deleteDocument, getExtractedText, getExtractedTextBlob } from '../api/documents'
import { askQuestionStream } from '../api/qa'
import StatusBadge from '../components/StatusBadge'
import { useDocumentStatusStream } from '../hooks/useDocumentStatusStream'
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
  const [streamActive, setStreamActive] = useState(false)
  const [tags, setTags] = useState<string[]>([])
  const [tagInput, setTagInput] = useState('')
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [tagsDirty, setTagsDirty] = useState(false)
  const [previewing, setPreviewing] = useState(false)
  const [downloading, setDownloading] = useState(false)
  const [fileActionError, setFileActionError] = useState<string | null>(null)
  const [extractedText, setExtractedText] = useState<string | null>(null)
  const [loadingText, setLoadingText] = useState(false)
  const [textError, setTextError] = useState<string | null>(null)
  const [textCopied, setTextCopied] = useState(false)
  const [downloadingText, setDownloadingText] = useState(false)
  const [question, setQuestion] = useState('')
  const [streamingAnswer, setStreamingAnswer] = useState<string | null>(null)
  const [qaLoading, setQaLoading] = useState(false)
  const [qaError, setQaError] = useState<string | null>(null)
  const abortRef = useRef<AbortController | null>(null)

  useEffect(() => {
    if (!id) return
    getDocument(id).then(d => {
      setDoc(d)
      setTags(d.tags.slice())
      setStreamActive(d.status === 'Imported')
    }).finally(() => setLoading(false))
  }, [id])

  useDocumentStatusStream(id, streamActive, (status) => {
    setDoc(prev => prev ? { ...prev, status } : prev)
    if (status === 'Indexed' || status === 'Failed') setStreamActive(false)
  })

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

  const handlePreview = async () => {
    if (!id) return

    setPreviewing(true)
    setFileActionError(null)

    try {
      const blob = await getDocumentPreviewBlob(id)
      const objectUrl = window.URL.createObjectURL(blob)

      const win = window.open(objectUrl, '_blank')
      if (!win) {
        // Popup blocker prevented the tab from opening — clean up and inform the user.
        window.URL.revokeObjectURL(objectUrl)
        setFileActionError('Preview was blocked — allow popups for this site and try again.')
        return
      }

      window.setTimeout(() => window.URL.revokeObjectURL(objectUrl), 60_000)
    } catch (e) {
      setFileActionError(e instanceof Error ? e.message : 'Failed to preview document')
    } finally {
      setPreviewing(false)
    }
  }

  const handleDownload = async () => {
    if (!id || !doc) return

    setDownloading(true)
    setFileActionError(null)

    try {
      const blob = await getDocumentDownloadBlob(id)
      const objectUrl = window.URL.createObjectURL(blob)
      const link = document.createElement('a')

      link.href = objectUrl
      link.download = doc.fileName
      document.body.appendChild(link)
      link.click()
      link.remove()

      window.setTimeout(() => window.URL.revokeObjectURL(objectUrl), 5_000)
    } catch (e) {
      setFileActionError(e instanceof Error ? e.message : 'Failed to download document')
    } finally {
      setDownloading(false)
    }
  }

  const handleLoadText = async () => {
    if (!id) return
    setLoadingText(true)
    setTextError(null)
    try {
      const text = await getExtractedText(id)
      setExtractedText(text)
    } catch (e) {
      setTextError(e instanceof Error ? e.message : 'Failed to load extracted text')
    } finally {
      setLoadingText(false)
    }
  }

  const handleCopyText = async () => {
    if (!extractedText) return
    await navigator.clipboard.writeText(extractedText)
    setTextCopied(true)
    setTimeout(() => setTextCopied(false), 2000)
  }

  const handleDownloadText = async () => {
    if (!id || !doc) return
    setDownloadingText(true)
    setTextError(null)
    try {
      const blob = await getExtractedTextBlob(id)
      const objectUrl = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = objectUrl
      link.download = doc.fileName.replace(/\.[^.]+$/, '') + '.txt'
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.setTimeout(() => window.URL.revokeObjectURL(objectUrl), 5_000)
    } catch (e) {
      setTextError(e instanceof Error ? e.message : 'Failed to download text')
    } finally {
      setDownloadingText(false)
    }
  }

  const handleAskDocument = async () => {
    if (!id || !question.trim()) return
    // Cancel any in-flight stream from a previous question.
    abortRef.current?.abort()
    const ctrl = new AbortController()
    abortRef.current = ctrl

    setQaLoading(true)
    setQaError(null)
    setStreamingAnswer('')
    try {
      await askQuestionStream(
        { question: question.trim(), documentId: id, maxDocuments: 12, maxContexts: 8 },
        token => setStreamingAnswer(prev => (prev ?? '') + token),
        ctrl.signal,
      )
    } catch (e) {
      if ((e as Error).name !== 'AbortError') {
        setStreamingAnswer(null)
        setQaError(e instanceof Error ? e.message : 'Failed to answer question')
      }
    } finally {
      setQaLoading(false)
    }
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
          {streamActive && (
            <span className="flex items-center gap-1 text-xs text-indigo-400">
              <span className="w-1.5 h-1.5 rounded-full bg-indigo-400 animate-pulse" />
              live
            </span>
          )}
        </div>
        <div className="flex flex-wrap gap-3 mb-6">
          <button
            onClick={handlePreview}
            disabled={previewing}
            className="flex items-center gap-2 bg-slate-800 hover:bg-slate-700 disabled:opacity-50 text-white text-sm px-4 py-2 rounded-lg transition-colors"
          >
            <Eye className="w-4 h-4" />{previewing ? 'Opening preview...' : 'Preview'}
          </button>
          <button
            onClick={handleDownload}
            disabled={downloading}
            className="flex items-center gap-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white text-sm px-4 py-2 rounded-lg transition-colors"
          >
            <Download className="w-4 h-4" />{downloading ? 'Preparing download...' : 'Download'}
          </button>
        </div>
        {fileActionError && (
          <div className="mb-6 rounded-lg border border-red-900/40 bg-red-900/10 px-4 py-3 text-sm text-red-300">
            {fileActionError}
          </div>
        )}
        <div className="grid grid-cols-2 gap-3">
          {[['Content Type', doc.contentType], ['File Size', formatBytes(doc.size)], ['Status', doc.status], ['ID', doc.id.slice(0, 8) + '...']].map(([label, value]) => (
            <div key={label} className="bg-slate-800/50 rounded-lg p-3">
              <p className="text-slate-500 text-xs mb-1">{label}</p>
              <p className="text-white text-sm font-medium truncate">{value}</p>
            </div>
          ))}
        </div>
      </div>

      {doc.contentType.startsWith('image/') && (
        <div className="bg-slate-900 rounded-2xl border border-slate-800 p-6 mb-4">
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-2">
              <FileText className="w-4 h-4 text-slate-400" />
              <h2 className="text-white font-medium">Extracted Text</h2>
              <span className="text-xs text-slate-500 bg-slate-800 px-2 py-0.5 rounded-full">OCR</span>
            </div>
            <div className="flex items-center gap-2">
              {extractedText !== null && extractedText.length > 0 && (
                <button
                  onClick={handleCopyText}
                  className="flex items-center gap-1.5 text-xs bg-slate-800 hover:bg-slate-700 text-slate-300 px-3 py-1.5 rounded-lg transition-colors"
                >
                  {textCopied ? <><Check className="w-3 h-3 text-green-400" />Copied</> : <><Copy className="w-3 h-3" />Copy</>}
                </button>
              )}
              <button
                onClick={handleDownloadText}
                disabled={downloadingText}
                className="flex items-center gap-1.5 text-xs bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white px-3 py-1.5 rounded-lg transition-colors"
              >
                <Download className="w-3 h-3" />{downloadingText ? 'Downloading...' : 'Download .txt'}
              </button>
            </div>
          </div>

          {doc.status !== 'Indexed' ? (
            <p className="text-slate-500 text-sm py-4 text-center">
              {doc.status === 'Imported'
                ? 'Text extraction is in progress…'
                : 'Text extraction is not available for this document.'}
            </p>
          ) : extractedText === null ? (
            <div className="text-center py-4">
              <button
                onClick={handleLoadText}
                disabled={loadingText}
                className="flex items-center gap-2 mx-auto bg-slate-800 hover:bg-slate-700 disabled:opacity-50 text-white text-sm px-4 py-2 rounded-lg transition-colors"
              >
                <Eye className="w-4 h-4" />{loadingText ? 'Loading…' : 'Show extracted text'}
              </button>
            </div>
          ) : extractedText === '' ? (
            <p className="text-slate-500 text-sm py-4 text-center">No text was extracted from this image.</p>
          ) : (
            <pre className="text-sm text-slate-300 whitespace-pre-wrap bg-slate-800/50 rounded-lg p-4 overflow-auto max-h-72 font-mono leading-relaxed">
              {extractedText}
            </pre>
          )}

          {textError && (
            <p className="text-red-400 text-xs mt-3">{textError}</p>
          )}
        </div>
      )}

      <div className="bg-slate-900 rounded-2xl border border-slate-800 p-6 mb-4">
        <div className="flex items-center gap-2 mb-3">
          <Sparkles className="w-4 h-4 text-indigo-400" />
          <h2 className="text-white font-medium">Ask this document</h2>
        </div>
        <div className="flex gap-2">
          <input
            value={question}
            onChange={e => setQuestion(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleAskDocument()}
            placeholder="e.g. When does this contract end?"
            className="flex-1 bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none"
          />
          <button
            onClick={handleAskDocument}
            disabled={!question.trim() || qaLoading}
            className="bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white text-sm px-4 py-2 rounded-lg transition-colors"
          >
            {qaLoading ? 'Asking...' : 'Ask'}
          </button>
        </div>
        {qaError && <p className="text-red-400 text-xs mt-3">{qaError}</p>}
        {streamingAnswer !== null && (
          <div className="mt-4 bg-slate-800/50 border border-slate-700 rounded-lg p-4">
            <p className="text-slate-100 text-sm leading-relaxed whitespace-pre-wrap">
              {streamingAnswer}
              {qaLoading && (
                <span className="inline-block w-0.5 h-4 bg-indigo-400 ml-0.5 align-text-bottom animate-pulse" />
              )}
            </p>
          </div>
        )}
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
