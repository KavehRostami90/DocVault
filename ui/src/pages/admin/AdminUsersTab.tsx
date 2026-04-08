import { useEffect, useState } from 'react'
import { Trash2 } from 'lucide-react'
import { adminApi, type AdminUser } from '../../api/admin'
import ConfirmDialog from '../../components/ConfirmDialog'

const ALL_ROLES = ['Admin', 'User']

export default function AdminUsersTab() {
  const [users, setUsers] = useState<AdminUser[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [pendingDelete, setPendingDelete] = useState<AdminUser | null>(null)
  const [toastMsg, setToastMsg] = useState('')

  useEffect(() => {
    load()
  }, [])

  function load() {
    setLoading(true)
    adminApi.listUsers()
      .then(setUsers)
      .catch(() => setError('Failed to load users.'))
      .finally(() => setLoading(false))
  }

  function toast(msg: string) {
    setToastMsg(msg)
    setTimeout(() => setToastMsg(''), 3000)
  }

  async function handleDeleteConfirm() {
    if (!pendingDelete) return
    try {
      await adminApi.deleteUser(pendingDelete.id)
      setUsers(u => u.filter(x => x.id !== pendingDelete.id))
      toast(`User "${pendingDelete.email}" deleted.`)
    } catch {
      toast('Failed to delete user.')
    } finally {
      setPendingDelete(null)
    }
  }

  async function handleRoleToggle(user: AdminUser, role: string) {
    const hasRole = user.roles.includes(role)
    const newRoles = hasRole
      ? user.roles.filter(r => r !== role)
      : [...user.roles, role]

    try {
      await adminApi.updateUserRoles(user.id, newRoles)
      setUsers(prev => prev.map(u => u.id === user.id ? { ...u, roles: newRoles } : u))
      toast(`Roles updated for ${user.email}.`)
    } catch {
      toast('Failed to update roles.')
    }
  }

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
              <th className="px-4 py-3 font-medium">User</th>
              <th className="px-4 py-3 font-medium">Roles</th>
              <th className="px-4 py-3 font-medium">Joined</th>
              <th className="px-4 py-3 font-medium">Type</th>
              <th className="px-4 py-3 font-medium">Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={5} className="px-4 py-8 text-center text-slate-500">Loading…</td></tr>
            ) : users.map(u => (
              <tr key={u.id} className="border-b border-slate-800/50 hover:bg-slate-800/30">
                <td className="px-4 py-3">
                  <div className="text-white font-medium">{u.displayName || '—'}</div>
                  <div className="text-slate-500 text-xs">{u.email}</div>
                </td>
                <td className="px-4 py-3">
                  <div className="flex gap-1 flex-wrap">
                    {ALL_ROLES.map(role => (
                      <button
                        key={role}
                        onClick={() => handleRoleToggle(u, role)}
                        title={u.roles.includes(role) ? `Remove ${role}` : `Add ${role}`}
                        className={`px-2 py-0.5 rounded text-xs font-medium border transition-colors ${
                          u.roles.includes(role)
                            ? role === 'Admin'
                              ? 'bg-indigo-600/20 text-indigo-400 border-indigo-600/40 hover:bg-indigo-600/10'
                              : 'bg-slate-700 text-slate-300 border-slate-600 hover:bg-slate-600'
                            : 'bg-transparent text-slate-600 border-slate-700 hover:text-slate-400'
                        }`}
                      >
                        {role}
                      </button>
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
                <td className="px-4 py-3">
                  <button
                    onClick={() => setPendingDelete(u)}
                    className="p-1.5 rounded text-slate-500 hover:text-red-400 hover:bg-red-500/10 transition-colors"
                    title="Delete user"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {pendingDelete && (
        <ConfirmDialog
          title="Delete user"
          message={`Are you sure you want to delete "${pendingDelete.email}"? This action cannot be undone.`}
          confirmLabel="Delete"
          onConfirm={handleDeleteConfirm}
          onCancel={() => setPendingDelete(null)}
        />
      )}
    </div>
  )
}
