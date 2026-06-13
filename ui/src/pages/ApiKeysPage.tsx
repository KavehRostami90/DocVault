import { useState, useEffect, useCallback } from 'react'
import { Key, Plus, Trash2, Copy, Check, AlertTriangle, X } from 'lucide-react'
import { apiKeysApi, type ApiKeyDto, type CreatedApiKey } from '../api/apiKeys'

function fmtDate(iso: string | null) {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function keyStatus(k: ApiKeyDto): { label: string; cls: string } {
  if (k.isRevoked)                                         return { label: 'Revoked',  cls: 'bg-slate-700/40 text-slate-400' }
  if (k.expiresAt && new Date(k.expiresAt) < new Date())   return { label: 'Expired',  cls: 'bg-amber-500/10 text-amber-400' }
  return { label: 'Active', cls: 'bg-emerald-500/10 text-emerald-400' }
}

// ── Create modal ────────────────────────────────────────────────────────────

interface CreateModalProps {
  onCreated: (result: CreatedApiKey) => void
  onClose: () => void
}

function CreateModal({ onCreated, onClose }: CreateModalProps) {
  const [name, setName] = useState('')
  const [expires, setExpires] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  async function submit() {
    const trimmed = name.trim()
    if (!trimmed) { setError('Name is required.'); return }
    setError('')
    setSaving(true)
    try {
      const result = await apiKeysApi.create(trimmed, expires || null)
      onCreated(result)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to create key.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div className="bg-slate-900 border border-slate-700 rounded-xl p-6 w-full max-w-md mx-4 space-y-4" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between">
          <h2 className="text-white font-semibold">New API key</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-white transition-colors"><X className="w-4 h-4" /></button>
        </div>

        <div className="space-y-3">
          <div>
            <label className="text-slate-400 text-xs block mb-1">Name <span className="text-rose-400">*</span></label>
            <input
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="e.g. CI pipeline"
              maxLength={100}
              autoFocus
              onKeyDown={e => { if (e.key === 'Enter') submit() }}
              className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-indigo-500 transition-colors"
            />
          </div>
          <div>
            <label className="text-slate-400 text-xs block mb-1">Expires (optional)</label>
            <input
              type="date"
              value={expires}
              onChange={e => setExpires(e.target.value)}
              min={new Date().toISOString().split('T')[0]}
              className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-indigo-500 transition-colors [color-scheme:dark]"
            />
          </div>
        </div>

        {error && <p className="text-rose-400 text-xs">{error}</p>}

        <div className="flex gap-2 pt-1">
          <button
            onClick={submit}
            disabled={saving}
            className="flex-1 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white text-sm rounded-lg transition-colors"
          >
            {saving ? 'Creating…' : 'Create key'}
          </button>
          <button
            onClick={onClose}
            disabled={saving}
            className="px-4 py-2 bg-slate-800 hover:bg-slate-700 disabled:opacity-50 text-slate-300 text-sm rounded-lg transition-colors"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Success modal (shows raw key once) ──────────────────────────────────────

interface SuccessModalProps {
  result: CreatedApiKey
  onClose: () => void
}

function SuccessModal({ result, onClose }: SuccessModalProps) {
  const [copied, setCopied] = useState(false)

  async function copy() {
    await navigator.clipboard.writeText(result.rawKey)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div className="bg-slate-900 border border-emerald-500/30 rounded-xl p-6 w-full max-w-md mx-4 space-y-4">
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-full bg-emerald-500/20 flex items-center justify-center flex-shrink-0">
            <Check className="w-4 h-4 text-emerald-400" />
          </div>
          <div>
            <h2 className="text-white font-semibold">Key created</h2>
            <p className="text-slate-400 text-xs">Copy it now — it won't be shown again.</p>
          </div>
        </div>

        <div className="bg-slate-800 border border-slate-700 rounded-lg p-3 flex items-center gap-2">
          <code className="flex-1 text-emerald-300 text-xs font-mono break-all">{result.rawKey}</code>
          <button
            onClick={copy}
            className="flex-shrink-0 p-1.5 rounded text-slate-400 hover:text-white hover:bg-slate-700 transition-colors"
            title="Copy to clipboard"
          >
            {copied ? <Check className="w-4 h-4 text-emerald-400" /> : <Copy className="w-4 h-4" />}
          </button>
        </div>

        <p className="text-slate-500 text-xs flex items-start gap-1.5">
          <AlertTriangle className="w-3.5 h-3.5 text-amber-400 flex-shrink-0 mt-0.5" />
          Send this key as the <code className="text-slate-300 bg-slate-800 px-1 rounded">X-Api-Key</code> header in API requests.
        </p>

        <button
          onClick={onClose}
          className="w-full py-2 bg-slate-800 hover:bg-slate-700 text-white text-sm rounded-lg transition-colors"
        >
          Done
        </button>
      </div>
    </div>
  )
}

// ── Revoke confirmation ─────────────────────────────────────────────────────

interface RevokeDialogProps {
  keyName: string
  onConfirm: () => void
  onClose: () => void
}

function RevokeDialog({ keyName, onConfirm, onClose }: RevokeDialogProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div className="bg-slate-900 border border-slate-700 rounded-xl p-6 w-full max-w-sm mx-4 space-y-4" onClick={e => e.stopPropagation()}>
        <h2 className="text-white font-semibold">Revoke key?</h2>
        <p className="text-slate-400 text-sm">
          <span className="text-white font-medium">{keyName}</span> will stop working immediately. This cannot be undone.
        </p>
        <div className="flex gap-2 pt-1">
          <button onClick={onConfirm} className="flex-1 py-2 bg-rose-600 hover:bg-rose-500 text-white text-sm rounded-lg transition-colors">
            Revoke
          </button>
          <button onClick={onClose} className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 text-sm rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Main page ───────────────────────────────────────────────────────────────

export default function ApiKeysPage() {
  const [keys, setKeys]           = useState<ApiKeyDto[]>([])
  const [loading, setLoading]     = useState(true)
  const [error, setError]         = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [created, setCreated]     = useState<CreatedApiKey | null>(null)
  const [revoking, setRevoking]   = useState<ApiKeyDto | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setKeys(await apiKeysApi.list())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to load API keys.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  function handleCreated(result: CreatedApiKey) {
    setShowCreate(false)
    setCreated(result)
    load()
  }

  async function handleRevoke(key: ApiKeyDto) {
    try {
      await apiKeysApi.revoke(key.id)
      setRevoking(null)
      load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to revoke key.')
      setRevoking(null)
    }
  }

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-white text-xl font-semibold">API Keys</h1>
          <p className="text-slate-400 text-sm mt-0.5">Authenticate API requests with <code className="text-slate-300 bg-slate-800 px-1 rounded text-xs">X-Api-Key</code> instead of a JWT token.</p>
        </div>
        <button
          onClick={() => setShowCreate(true)}
          className="flex items-center gap-2 px-3 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm rounded-lg transition-colors"
        >
          <Plus className="w-4 h-4" />
          New key
        </button>
      </div>

      {error && (
        <div className="bg-rose-500/10 border border-rose-500/30 rounded-lg px-4 py-3 text-rose-400 text-sm">
          {error}
        </div>
      )}

      {loading ? (
        <div className="text-slate-500 text-sm">Loading…</div>
      ) : keys.length === 0 ? (
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-10 flex flex-col items-center gap-3">
          <Key className="w-8 h-8 text-slate-600" />
          <p className="text-slate-400 text-sm">No API keys yet.</p>
          <button
            onClick={() => setShowCreate(true)}
            className="text-indigo-400 hover:text-indigo-300 text-sm transition-colors"
          >
            Create your first key →
          </button>
        </div>
      ) : (
        <div className="bg-slate-900 border border-slate-800 rounded-xl divide-y divide-slate-800">
          {keys.map(k => {
            const { label, cls } = keyStatus(k)
            const isActive = label === 'Active'
            return (
              <div key={k.id} className="flex items-center gap-4 px-5 py-4">
                <div className="w-8 h-8 rounded-lg bg-slate-800 flex items-center justify-center flex-shrink-0">
                  <Key className="w-4 h-4 text-slate-400" />
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-white text-sm font-medium truncate">{k.name}</span>
                    <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${cls}`}>{label}</span>
                  </div>
                  <div className="flex flex-wrap gap-x-4 mt-0.5">
                    <span className="text-slate-500 text-xs font-mono">{k.keyPrefix}…</span>
                    <span className="text-slate-500 text-xs">Created {fmtDate(k.createdAt)}</span>
                    {k.expiresAt && <span className="text-slate-500 text-xs">Expires {fmtDate(k.expiresAt)}</span>}
                    {k.lastUsedAt && <span className="text-slate-500 text-xs">Last used {fmtDate(k.lastUsedAt)}</span>}
                  </div>
                </div>

                {isActive && (
                  <button
                    onClick={() => setRevoking(k)}
                    className="flex-shrink-0 flex items-center gap-1.5 px-2.5 py-1.5 text-rose-400 hover:text-white hover:bg-rose-500/20 text-xs rounded-lg transition-colors"
                    title="Revoke key"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                    Revoke
                  </button>
                )}
              </div>
            )
          })}
        </div>
      )}

      {showCreate  && <CreateModal onCreated={handleCreated} onClose={() => setShowCreate(false)} />}
      {created     && <SuccessModal result={created} onClose={() => setCreated(null)} />}
      {revoking    && <RevokeDialog keyName={revoking.name} onConfirm={() => handleRevoke(revoking)} onClose={() => setRevoking(null)} />}
    </div>
  )
}
