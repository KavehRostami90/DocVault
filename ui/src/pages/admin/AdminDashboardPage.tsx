import { useEffect, useState } from 'react'
import { get } from '../../api/client'
import type { PageResponse } from '../../types/index'
import { Users, FileText, Shield } from 'lucide-react'

interface AdminUser {
  id: string
  email: string
  displayName: string
  isGuest: boolean
  createdAt: string
  roles: string[]
}

interface AdminDocument {
  id: string
  title: string
  status: string
  size: number
  createdAt: string
  ownerId: string | null
}

type Tab = 'users' | 'documents'

export default function AdminDashboardPage() {
  const [tab, setTab] = useState<Tab>('users')
  const [users, setUsers] = useState<AdminUser[]>([])
  const [docs, setDocs] = useState<AdminDocument[]>([])
  const [loadingUsers, setLoadingUsers] = useState(true)
  const [loadingDocs, setLoadingDocs] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    get<AdminUser[]>('/admin/users')
      .then(setUsers)
      .catch(() => setError('Failed to load users.'))
      .finally(() => setLoadingUsers(false))

    get<PageResponse<AdminDocument>>('/admin/documents?size=100')
      .then(r => setDocs(r.items))
      .catch(() => setError('Failed to load documents.'))
      .finally(() => setLoadingDocs(false))
  }, [])

  const formatSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Shield className="w-6 h-6 text-indigo-400" />
        <h1 className="text-white text-2xl font-semibold">Admin Dashboard</h1>
      </div>

      {error && (
        <div className="bg-red-500/10 border border-red-500/30 text-red-400 text-sm rounded-lg px-4 py-3">
          {error}
        </div>
      )}

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
        <StatCard label="Total Users" value={loadingUsers ? '…' : String(users.length)} icon={<Users className="w-5 h-5" />} />
        <StatCard label="Guest Users" value={loadingUsers ? '…' : String(users.filter(u => u.isGuest).length)} icon={<Users className="w-5 h-5" />} />
        <StatCard label="Total Documents" value={loadingDocs ? '…' : String(docs.length)} icon={<FileText className="w-5 h-5" />} />
      </div>

      <div className="flex gap-1 bg-slate-900 rounded-lg p-1 w-fit border border-slate-800">
        {(['users', 'documents'] as Tab[]).map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-4 py-1.5 rounded-md text-sm font-medium transition-colors capitalize ${
              tab === t ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-white'
            }`}
          >
            {t}
          </button>
        ))}
      </div>

      {tab === 'users' && (
        <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-800 text-slate-400 text-left">
                <th className="px-4 py-3 font-medium">User</th>
                <th className="px-4 py-3 font-medium">Roles</th>
                <th className="px-4 py-3 font-medium">Joined</th>
                <th className="px-4 py-3 font-medium">Type</th>
              </tr>
            </thead>
            <tbody>
              {loadingUsers ? (
                <tr><td colSpan={4} className="px-4 py-8 text-center text-slate-500">Loading…</td></tr>
              ) : users.map(u => (
                <tr key={u.id} className="border-b border-slate-800/50 hover:bg-slate-800/30">
                  <td className="px-4 py-3">
                    <div className="text-white font-medium">{u.displayName || '—'}</div>
                    <div className="text-slate-500 text-xs">{u.email}</div>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex gap-1 flex-wrap">
                      {u.roles.map(r => (
                        <span key={r} className={`px-2 py-0.5 rounded text-xs font-medium ${
                          r === 'Admin' ? 'bg-indigo-600/20 text-indigo-400' : 'bg-slate-700 text-slate-300'
                        }`}>{r}</span>
                      ))}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-slate-400">
                    {new Date(u.createdAt).toLocaleDateString()}
                  </td>
                  <td className="px-4 py-3">
                    {u.isGuest
                      ? <span className="text-amber-400 text-xs">Guest</span>
                      : <span className="text-emerald-400 text-xs">Registered</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'documents' && (
        <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-800 text-slate-400 text-left">
                <th className="px-4 py-3 font-medium">Title</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Size</th>
                <th className="px-4 py-3 font-medium">Created</th>
              </tr>
            </thead>
            <tbody>
              {loadingDocs ? (
                <tr><td colSpan={4} className="px-4 py-8 text-center text-slate-500">Loading…</td></tr>
              ) : docs.map(d => (
                <tr key={d.id} className="border-b border-slate-800/50 hover:bg-slate-800/30">
                  <td className="px-4 py-3 text-white font-medium max-w-xs truncate">{d.title}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded text-xs font-medium ${
                      d.status === 'Indexed' ? 'bg-emerald-500/15 text-emerald-400'
                      : d.status === 'Failed' ? 'bg-red-500/15 text-red-400'
                      : 'bg-slate-700 text-slate-300'
                    }`}>{d.status}</span>
                  </td>
                  <td className="px-4 py-3 text-slate-400">{formatSize(d.size)}</td>
                  <td className="px-4 py-3 text-slate-400">{new Date(d.createdAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function StatCard({ label, value, icon }: { label: string; value: string; icon: React.ReactNode }) {
  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-4">
      <div className="flex items-center gap-2 text-slate-400 mb-2">
        {icon}
        <span className="text-xs font-medium uppercase tracking-wide">{label}</span>
      </div>
      <div className="text-white text-2xl font-bold">{value}</div>
    </div>
  )
}
