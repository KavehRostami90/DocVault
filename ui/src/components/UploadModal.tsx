import { useState, useRef } from 'react'
import { X, Upload, Plus, Tag } from 'lucide-react'
import { uploadDocument } from '../api/documents'

const ACCEPTED_MIME_TYPES = [
  'application/pdf',
  'text/plain',
  'text/markdown',
  'text/x-markdown',
  'application/json',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
]

const ACCEPTED_EXTENSIONS = '.pdf,.txt,.md,.docx,.json'

const FILE_TYPE_LABELS: Record<string, string> = {
  'application/pdf': 'PDF',
  'text/plain': 'TXT',
  'text/markdown': 'MD',
  'text/x-markdown': 'MD',
  'application/json': 'JSON',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': 'DOCX',
}

function getFileTypeLabel(mimeType: string): string {
  return FILE_TYPE_LABELS[mimeType] ?? mimeType
}

interface Props {
  onClose: () => void
  onUploaded: (id: string) => void
}

export default function UploadModal({ onClose, onUploaded }: Props) {
  const [title, setTitle] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [tags, setTags] = useState<string[]>([])
  const [tagInput, setTagInput] = useState('')
  const [dragging, setDragging] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const handleFile = (f: File) => {
    if (!ACCEPTED_MIME_TYPES.includes(f.type)) {
      setError(`Unsupported file type "${f.type || f.name.split('.').pop()}". Allowed: PDF, TXT, MD, DOCX`)
      return
    }
    setError(null)
    setFile(f)
    setTitle(prev => prev || f.name.replace(/\.[^.]+$/, ''))
  }

  const addTag = () => {
    const t = tagInput.trim().toLowerCase()
    if (t && !tags.includes(t)) setTags(prev => [...prev, t])
    setTagInput('')
  }

  const submit = async () => {
    if (!file || !title.trim()) return
    setLoading(true)
    setError(null)
    try {
      const { id } = await uploadDocument(title.trim(), tags, file)
      onUploaded(id)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Upload failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
      <div className="w-full max-w-lg bg-slate-900 rounded-2xl border border-slate-700 shadow-2xl">
        <div className="flex items-center justify-between p-6 border-b border-slate-800">
          <h2 className="text-lg font-semibold text-white">Upload Document</h2>
          <button onClick={onClose} className="text-slate-500 hover:text-white transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 space-y-5">
          <div
            onClick={() => inputRef.current?.click()}
            onDragOver={e => { e.preventDefault(); setDragging(true) }}
            onDragLeave={() => setDragging(false)}
            onDrop={e => { e.preventDefault(); setDragging(false); const f = e.dataTransfer.files[0]; if (f) handleFile(f) }}
            className={`cursor-pointer rounded-xl border-2 border-dashed p-8 text-center transition-colors ${
              dragging ? 'border-indigo-500 bg-indigo-500/10' : 'border-slate-700 hover:border-slate-600 hover:bg-slate-800/50'
            }`}
          >
            <input ref={inputRef} type="file" className="hidden" accept={ACCEPTED_EXTENSIONS} onChange={e => { const f = e.target.files?.[0]; if (f) handleFile(f) }} />
            {file ? (
              <div className="flex items-center justify-center gap-3">
                <div className="w-10 h-10 bg-indigo-600/20 rounded-lg flex items-center justify-center shrink-0">
                  <span className="text-indigo-400 text-xs font-bold">{getFileTypeLabel(file.type)}</span>
                </div>
                <div className="text-left">
                  <p className="text-white font-medium text-sm">{file.name}</p>
                  <p className="text-slate-500 text-xs">{(file.size / 1024).toFixed(1)} KB</p>
                </div>
              </div>
            ) : (
              <>
                <Upload className="w-8 h-8 text-slate-500 mx-auto mb-3" />
                <p className="text-slate-400 text-sm">Drop a file here or <span className="text-indigo-400">browse</span></p>
                <p className="text-slate-600 text-xs mt-1">PDF, DOCX, TXT, MD, JSON · Max 50 MB</p>
              </>
            )}
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-400 mb-1.5">Title</label>
            <input
              value={title}
              onChange={e => setTitle(e.target.value)}
              placeholder="Document title"
              className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-400 mb-1.5">Tags</label>
            <div className="flex gap-2">
              <div className="flex-1 flex items-center gap-2 bg-slate-800 border border-slate-700 rounded-lg px-3 py-2">
                <Tag className="w-3.5 h-3.5 text-slate-500 shrink-0" />
                <input
                  value={tagInput}
                  onChange={e => setTagInput(e.target.value)}
                  onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addTag() } }}
                  placeholder="Add tag and press Enter..."
                  className="flex-1 bg-transparent text-sm text-white placeholder-slate-500 focus:outline-none"
                />
              </div>
              <button onClick={addTag} className="px-3 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors">
                <Plus className="w-4 h-4" />
              </button>
            </div>
            {tags.length > 0 && (
              <div className="flex flex-wrap gap-2 mt-2">
                {tags.map(t => (
                  <span key={t} className="inline-flex items-center gap-1 bg-indigo-600/20 text-indigo-400 text-xs px-2 py-1 rounded-full">
                    {t}
                    <button onClick={() => setTags(tags.filter(x => x !== t))} className="hover:text-white"><X className="w-3 h-3" /></button>
                  </span>
                ))}
              </div>
            )}
          </div>

          {error && (
            <div className="bg-red-500/10 border border-red-500/30 text-red-400 text-sm rounded-lg px-4 py-3 space-y-1">
              {error.split('\n').map((line, i) => <p key={i}>{line}</p>)}
            </div>
          )}
        </div>

        <div className="flex items-center justify-end gap-3 p-6 border-t border-slate-800">
          <button onClick={onClose} className="px-4 py-2 text-sm text-slate-400 hover:text-white transition-colors">Cancel</button>
          <button
            onClick={submit}
            disabled={!file || !title.trim() || loading}
            className="px-5 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-medium rounded-lg transition-colors flex items-center gap-2"
          >
            {loading ? <><span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />Uploading...</> : <><Upload className="w-4 h-4" />Upload</>}
          </button>
        </div>
      </div>
    </div>
  )
}
