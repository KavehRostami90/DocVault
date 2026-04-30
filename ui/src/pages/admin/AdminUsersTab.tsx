import React, { useEffect, useMemo, useState } from 'react'
import { Trash2, KeyRound, X } from 'lucide-react'
import { adminApi, type AdminUser } from '../../api/admin'
import ConfirmDialog from '../../components/ConfirmDialog'
import Pagination from '../../components/Pagination'
import type { AdminUserFilter } from './adminFilters'

const ALL_ROLES = ['Admin', 'User']
const PAGE_SIZE = 15

interface Props {
  filter?: AdminUserFilter
  onClearFilter?: () => void
}

export default function AdminUsersTab({ filter = 'all', onClearFilter }: Props) {
  const [users, setUsers] = useState<AdminUser[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)
  const [pendingDelete, setPendingDelete] = useState<AdminUser | null>(null)
  const [toastMsg, setToastMsg] = useState('')
  const [pwTarget, setPwTarget] = useState<AdminUser | null>(null)
  const [pwValue, setPwValue] = useState('')
  const [pwError, setPwError] = useState('')
  const [pwSaving, setPwSaving] = useState(false)

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

  function openPw(user: AdminUser) {
    setPwTarget(user)
    setPwValue('')
    setPwError('')
  }

  async function handleSetPassword() {
    if (!pwTarget) return
    if (pwValue.length < 8) { setPwError('Min 8 characters.'); return }
    setPwError('')
    setPwSaving(true)
    try {
      await adminApi.setUserPassword(pwTarget.id, pwValue)
      toast(`Password updated for "${pwTarget.email}".`)
      setPwTarget(null)
    } catch {
      setPwError('Failed to set password.')
    } finally {
      setPwSaving(false)
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

  const filteredUsers = useMemo(() => {
    switch (filter) {
      case 'registered':
        return users.filter(u => !u.isGuest)
      case 'guests':
        return users.filter(u => u.isGuest)
      case 'admins':
        return users.filter(u => u.roles.includes('Admin'))
      default:
        return users
    }
  }, [filter, users])

  // Reset to page 1 whenever filter changes
  useEffect(() => { setPage(1) }, [filter])

  const totalPages = Math.ceil(filteredUsers.length / PAGE_SIZE)
  const pagedUsers = filteredUsers.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)
  const filterLabel = userFilterLabel(filter)

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
          <p className="text-sm text-slate-500">{filteredUsers.length} user{filteredUsers.length !== 1 ? 's' : ''}{totalPages > 1 ? ` · page ${page} of ${totalPages}` : ''}</p>
        </div>
        {filter !== 'all' && onClearFilter && (
          <button
            type="button"
            onClick={onClearFilter}
            className="rounded-lg border border-slate-700 px-3 py-1.5 text-sm text-slate-300 transition-colors hover:border-slate-600 hover:text-white"
          >
            Show all users
          </button>
        )}
      </div>

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
            ) : filteredUsers.length === 0 ? (
              <tr><td colSpan={5} className="px-4 py-8 text-center text-slate-500">No users found for this category.</td></tr>
            ) : pagedUsers.map(u => (
              <React.Fragment key={u.id}>
              <tr className="border-b border-slate-800/50 hover:bg-slate-800/30">
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
                  <div className="flex items-center gap-1">
                    {!u.isGuest && (
                      <button
                        onClick={() => openPw(u)}
                        className="p-1.5 rounded text-slate-500 hover:text-amber-400 hover:bg-amber-500/10 transition-colors"
                        title="Set password"
                      >
                        <KeyRound className="w-4 h-4" />
                      </button>
                    )}
                    <button
                      onClick={() => setPendingDelete(u)}
                      className="p-1.5 rounded text-slate-500 hover:text-red-400 hover:bg-red-500/10 transition-colors"
                      title="Delete user"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                </td>
              </tr>
              {pwTarget?.id === u.id && (
                <tr className="bg-slate-800/40 border-b border-slate-800/50">
                  <td colSpan={5} className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      <KeyRound className="w-4 h-4 text-amber-400 flex-shrink-0" />
                      <span className="text-amber-400 text-xs font-medium">Set password for {pwTarget.email}</span>
                      <input
                        type="password"
                        value={pwValue}
                        onChange={e => setPwValue(e.target.value)}
                        placeholder="New password (min 8 chars)"
                        autoFocus
                        className="flex-1 bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-white text-xs focus:outline-none focus:border-amber-500 transition-colors"
                        onKeyDown={e => { if (e.key === 'Enter') handleSetPassword(); if (e.key === 'Escape') setPwTarget(null) }}
                      />
                      {pwError && <span className="text-rose-400 text-xs">{pwError}</span>}
                      <button
                        onClick={handleSetPassword}
                        disabled={pwSaving}
                        className="px-3 py-1.5 bg-amber-600 hover:bg-amber-500 disabled:opacity-50 text-white text-xs rounded-lg transition-colors flex-shrink-0"
                      >
                        {pwSaving ? 'Saving…' : 'Set'}
                      </button>
                      <button
                        onClick={() => setPwTarget(null)}
                        className="p-1.5 rounded text-slate-500 hover:text-white transition-colors flex-shrink-0"
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              )}
              </React.Fragment>
            ))}
          </tbody>
        </table>
      </div>

      <Pagination
        page={page}
        totalPages={totalPages}
        onPageChange={setPage}
      />

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

function userFilterLabel(filter: AdminUserFilter): string {
  switch (filter) {
    case 'registered':
      return 'Registered Users'
    case 'guests':
      return 'Guest Users'
    case 'admins':
      return 'Admin Users'
    default:
      return 'All Users'
  }
}
