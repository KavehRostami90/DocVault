import { useCallback, useEffect, useRef, useState } from 'react'
import { X, Upload, Plus, Tag, CheckCircle, AlertCircle } from 'lucide-react'
import { uploadDocument } from '../api/documents'
import { getUploadSettings } from '../api/settings'

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
  return FILE_TYPE_LABELS[mimeType] ?? (mimeType.split('/').pop()?.toUpperCase() ?? '?')
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

type UploadStatus = 'pending' | 'uploading' | 'done' | 'failed'

interface FileEntry {
  key: number
  file: File
  title: string
  tags: string[]
  tagInput: string
  validationError: string | null
  uploadStatus: UploadStatus
  uploadError: string | null
  uploadedId?: string
}

let entryCounter = 0

function createEntry(file: File, maxFileSizeBytes: number): FileEntry {
  let validationError: string | null = null
  if (file.size > maxFileSizeBytes) {
    validationError = `Too large (${formatFileSize(file.size)}). Max ${formatFileSize(maxFileSizeBytes)}.`
  } else if (!ACCEPTED_MIME_TYPES.includes(file.type)) {
    validationError = `Unsupported type "${file.type || file.name.split('.').pop()}". Allowed: PDF, TXT, MD, DOCX, JSON`
  }
  return {
    key: ++entryCounter,
    file,
    title: file.name.replace(/\.[^.]+$/, ''),
    tags: [],
    tagInput: '',
    validationError,
    uploadStatus: 'pending',
    uploadError: null,
  }
}

interface Props {
  onClose: () => void
  onUploaded: (ids: string[]) => void
}

export default function UploadModal({ onClose, onUploaded }: Props) {
  const [entries, setEntries] = useState<FileEntry[]>([])
  const [dragging, setDragging] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [uploadDone, setUploadDone] = useState(false)
  const [settingsLoading, setSettingsLoading] = useState(true)
  const [maxFileSizeBytes, setMaxFileSizeBytes] = useState<number | null>(null)
  const [maxUploadCount, setMaxUploadCount] = useState<number | null>(null)
  const [settingsError, setSettingsError] = useState<string | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    let cancelled = false
    getUploadSettings()
      .then(settings => {
        if (cancelled) return
        setMaxFileSizeBytes(settings.maxFileSizeBytes)
        setMaxUploadCount(settings.maxUploadCount)
      })
      .catch(() => {
        if (cancelled) return
        setSettingsError('Failed to load upload settings. Please try again.')
      })
      .finally(() => {
        if (cancelled) return
        setSettingsLoading(false)
      })
    return () => { cancelled = true }
  }, [])

  const addFiles = useCallback((files: File[]) => {
    if (maxFileSizeBytes === null || maxUploadCount === null) return
    setEntries(prev => {
      const existingNames = new Set(prev.map(e => e.file.name))
      const available = maxUploadCount - prev.length
      if (available <= 0) return prev
      const newEntries = files
        .filter(f => !existingNames.has(f.name))
        .slice(0, available)
        .map(f => createEntry(f, maxFileSizeBytes))
      return [...prev, ...newEntries]
    })
  }, [maxFileSizeBytes, maxUploadCount])

  const canSelectFile = !settingsLoading && maxFileSizeBytes !== null && maxUploadCount !== null && !uploading && entries.length < (maxUploadCount ?? Infinity)

  const pendingValid = entries.filter(e => e.uploadStatus === 'pending' && !e.validationError)
  const canUpload = !uploading && !settingsLoading && maxFileSizeBytes !== null && maxUploadCount !== null && pendingValid.length > 0
  const atLimit = maxUploadCount !== null && entries.length >= maxUploadCount

  const submit = async () => {
    if (!canUpload) return
    setUploading(true)
    const uploadedIds: string[] = []
    let hasFailed = false

    for (const entry of pendingValid) {
      setEntries(prev => prev.map(e => e.key === entry.key ? { ...e, uploadStatus: 'uploading' } : e))
      try {
        const { id } = await uploadDocument(entry.title.trim() || entry.file.name, entry.tags, entry.file)
        uploadedIds.push(id)
        setEntries(prev => prev.map(e => e.key === entry.key ? { ...e, uploadStatus: 'done', uploadedId: id } : e))
      } catch (err) {
        hasFailed = true
        setEntries(prev => prev.map(e => e.key === entry.key ? {
          ...e,
          uploadStatus: 'failed',
          uploadError: err instanceof Error ? err.message : 'Upload failed',
        } : e))
      }
    }

    setUploading(false)

    if (!hasFailed && uploadedIds.length > 0) {
      onUploaded(uploadedIds)
    } else {
      setUploadDone(true)
    }
  }

  const handleDone = () => {
    const ids = entries.filter(e => e.uploadStatus === 'done' && e.uploadedId).map(e => e.uploadedId!)
    if (ids.length > 0) {
      onUploaded(ids)
    } else {
      onClose()
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
      <div className="w-full max-w-2xl bg-slate-900 rounded-2xl border border-slate-700 shadow-2xl flex flex-col max-h-[90vh]">

        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-slate-800 shrink-0">
          <div>
            <h2 className="text-lg font-semibold text-white">Upload Documents</h2>
            {entries.length > 0 && (
              <p className="text-slate-500 text-xs mt-0.5">
                {entries.length}{maxUploadCount !== null ? ` / ${maxUploadCount}` : ''} file{entries.length !== 1 ? 's' : ''} selected
              </p>
            )}
          </div>
          <button onClick={onClose} disabled={uploading} className="text-slate-500 hover:text-white transition-colors disabled:opacity-40">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Scrollable body */}
        <div className="flex-1 overflow-y-auto p-6 space-y-5 min-h-0">

          {/* Drop zone */}
          <div
            onClick={() => { if (canSelectFile) inputRef.current?.click() }}
            onDragOver={e => { if (!canSelectFile) return; e.preventDefault(); setDragging(true) }}
            onDragLeave={() => setDragging(false)}
            onDrop={e => {
              e.preventDefault(); setDragging(false)
              if (!canSelectFile) return
              addFiles(Array.from(e.dataTransfer.files))
            }}
            className={`rounded-xl border-2 border-dashed p-6 text-center transition-colors ${
              canSelectFile ? 'cursor-pointer' : 'cursor-not-allowed opacity-75'
            } ${
              dragging
                ? 'border-indigo-500 bg-indigo-500/10'
                : canSelectFile
                  ? 'border-slate-700 hover:border-slate-600 hover:bg-slate-800/50'
                  : 'border-slate-700'
            }`}
          >
            <input
              ref={inputRef}
              type="file"
              className="hidden"
              accept={ACCEPTED_EXTENSIONS}
              multiple
              disabled={!canSelectFile}
              onChange={e => {
                if (e.target.files) addFiles(Array.from(e.target.files))
                e.target.value = ''
              }}
            />
            <Upload className="w-7 h-7 text-slate-500 mx-auto mb-2" />
            <p className="text-slate-400 text-sm">
              {settingsLoading
                ? 'Loading upload settings...'
                : atLimit
                  ? <span className="text-amber-400">Limit reached ({maxUploadCount} files max). Remove a file to add another.</span>
                  : <><span className="text-indigo-400">Click to browse</span> or drag &amp; drop — multiple files supported</>}
            </p>
            <p className="text-slate-600 text-xs mt-1">
              PDF, DOCX, TXT, MD, JSON
              {maxFileSizeBytes !== null ? ` · Max ${formatFileSize(maxFileSizeBytes)} each` : ''}
              {maxUploadCount !== null ? ` · Up to ${maxUploadCount} files` : ''}
            </p>
          </div>

          {settingsError && (
            <div className="bg-red-500/10 border border-red-500/30 text-red-400 text-sm rounded-lg px-4 py-3">
              {settingsError}
            </div>
          )}

          {/* File list */}
          {entries.length > 0 && (
            <div className="space-y-3">
              <h3 className="text-xs font-medium text-slate-400 uppercase tracking-wider">
                Files to upload
              </h3>
              {entries.map(entry => (
                <FileCard
                  key={entry.key}
                  entry={entry}
                  uploading={uploading}
                  onRemove={() => setEntries(prev => prev.filter(e => e.key !== entry.key))}
                  onTitleChange={title => setEntries(prev => prev.map(e => e.key === entry.key ? { ...e, title } : e))}
                  onTagInputChange={tagInput => setEntries(prev => prev.map(e => e.key === entry.key ? { ...e, tagInput } : e))}
                  onAddTag={() => setEntries(prev => prev.map(e => {
                    if (e.key !== entry.key) return e
                    const t = e.tagInput.trim().toLowerCase()
                    if (!t || e.tags.includes(t)) return { ...e, tagInput: '' }
                    return { ...e, tags: [...e.tags, t], tagInput: '' }
                  }))}
                  onRemoveTag={tag => setEntries(prev => prev.map(e => e.key === entry.key ? { ...e, tags: e.tags.filter(t => t !== tag) } : e))}
                />
              ))}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between gap-3 p-6 border-t border-slate-800 shrink-0">
          <p className="text-xs text-slate-600">
            {pendingValid.length} ready
            {entries.filter(e => e.validationError).length > 0 && ` · ${entries.filter(e => e.validationError).length} invalid`}
            {entries.filter(e => e.uploadStatus === 'done').length > 0 && ` · ${entries.filter(e => e.uploadStatus === 'done').length} uploaded`}
            {entries.filter(e => e.uploadStatus === 'failed').length > 0 && ` · ${entries.filter(e => e.uploadStatus === 'failed').length} failed`}
          </p>
          <div className="flex gap-3">
            {uploadDone ? (
              <button
                onClick={handleDone}
                className="px-5 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-lg transition-colors"
              >
                Done
              </button>
            ) : (
              <>
                <button onClick={onClose} disabled={uploading} className="px-4 py-2 text-sm text-slate-400 hover:text-white transition-colors disabled:opacity-50">
                  Cancel
                </button>
                <button
                  onClick={submit}
                  disabled={!canUpload}
                  className="px-5 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-medium rounded-lg transition-colors flex items-center gap-2"
                >
                  {uploading
                    ? <><span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />Uploading...</>
                    : <><Upload className="w-4 h-4" />Upload {pendingValid.length > 0 ? `${pendingValid.length} File${pendingValid.length !== 1 ? 's' : ''}` : ''}</>}
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

// ─── FileCard ────────────────────────────────────────────────────────────────

interface FileCardProps {
  entry: FileEntry
  uploading: boolean
  onRemove: () => void
  onTitleChange: (title: string) => void
  onTagInputChange: (input: string) => void
  onAddTag: () => void
  onRemoveTag: (tag: string) => void
}

function FileCard({ entry, uploading, onRemove, onTitleChange, onTagInputChange, onAddTag, onRemoveTag }: FileCardProps) {
  const isLocked = uploading || entry.uploadStatus === 'done' || entry.uploadStatus === 'uploading'

  const borderClass =
    entry.validationError || entry.uploadStatus === 'failed'
      ? 'border-red-500/30 bg-red-500/5'
      : entry.uploadStatus === 'done'
        ? 'border-green-500/30 bg-green-500/5'
        : 'border-slate-700 bg-slate-800/30'

  return (
    <div className={`rounded-xl border p-4 space-y-3 transition-colors ${borderClass}`}>

      {/* File info row */}
      <div className="flex items-center gap-3">
        <div className="w-9 h-9 bg-indigo-600/20 rounded-lg flex items-center justify-center shrink-0">
          {entry.uploadStatus === 'uploading' ? (
            <span className="w-4 h-4 border-2 border-indigo-400/30 border-t-indigo-400 rounded-full animate-spin" />
          ) : entry.uploadStatus === 'done' ? (
            <CheckCircle className="w-4 h-4 text-green-400" />
          ) : entry.validationError || entry.uploadStatus === 'failed' ? (
            <AlertCircle className="w-4 h-4 text-red-400" />
          ) : (
            <span className="text-indigo-400 text-xs font-bold">{getFileTypeLabel(entry.file.type)}</span>
          )}
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-white text-sm font-medium truncate">{entry.file.name}</p>
          <p className="text-slate-500 text-xs">{formatFileSize(entry.file.size)}</p>
        </div>
        {!isLocked && (
          <button onClick={onRemove} className="text-slate-500 hover:text-red-400 transition-colors">
            <X className="w-4 h-4" />
          </button>
        )}
      </div>

      {entry.validationError && (
        <p className="text-red-400 text-xs">{entry.validationError}</p>
      )}

      {entry.uploadStatus === 'failed' && entry.uploadError && (
        <p className="text-red-400 text-xs">{entry.uploadError}</p>
      )}

      {entry.uploadStatus === 'done' && (
        <p className="text-green-400 text-xs">Uploaded successfully</p>
      )}

      {/* Title + tags (only for valid, not-yet-done files) */}
      {!entry.validationError && entry.uploadStatus !== 'done' && (
        <>
          <input
            value={entry.title}
            onChange={e => onTitleChange(e.target.value)}
            disabled={isLocked}
            placeholder="Document title"
            className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 disabled:opacity-60"
          />

          <div>
            <div className="flex gap-2">
              <div className="flex-1 flex items-center gap-2 bg-slate-800 border border-slate-700 rounded-lg px-3 py-1.5">
                <Tag className="w-3 h-3 text-slate-500 shrink-0" />
                <input
                  value={entry.tagInput}
                  onChange={e => onTagInputChange(e.target.value)}
                  onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); onAddTag() } }}
                  disabled={isLocked}
                  placeholder="Add tag and press Enter..."
                  className="flex-1 bg-transparent text-xs text-white placeholder-slate-500 focus:outline-none disabled:opacity-60"
                />
              </div>
              <button
                onClick={onAddTag}
                disabled={isLocked}
                className="px-2.5 py-1.5 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors disabled:opacity-50"
              >
                <Plus className="w-3.5 h-3.5" />
              </button>
            </div>
            {entry.tags.length > 0 && (
              <div className="flex flex-wrap gap-1.5 mt-2">
                {entry.tags.map(t => (
                  <span key={t} className="inline-flex items-center gap-1 bg-indigo-600/20 text-indigo-400 text-xs px-2 py-0.5 rounded-full">
                    {t}
                    <button onClick={() => onRemoveTag(t)} disabled={isLocked} className="hover:text-white disabled:opacity-50">
                      <X className="w-2.5 h-2.5" />
                    </button>
                  </span>
                ))}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  )
}
