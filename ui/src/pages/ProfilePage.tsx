import { useState, useEffect } from 'react'
import { User, Mail, Shield, Calendar, Edit2, Lock, KeyRound, Check, X, HardDrive } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import { authApi, getStorageUsage, type StorageUsage } from '../api/auth'

export default function ProfilePage() {
  const { user, getToken, updateProfile } = useAuth()

  const [editingName, setEditingName] = useState(false)
  const [displayName, setDisplayName] = useState(user?.displayName ?? '')

  const [storage, setStorage] = useState<StorageUsage | null>(null)

  useEffect(() => {
    const token = getToken()
    if (!token) return
    getStorageUsage(token).then(setStorage).catch(() => {})
  }, [getToken])
  const [nameError, setNameError] = useState('')
  const [nameSaving, setNameSaving] = useState(false)

  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [pwError, setPwError] = useState('')
  const [pwSuccess, setPwSuccess] = useState(false)
  const [pwSaving, setPwSaving] = useState(false)

  const [resetPassword, setResetPassword] = useState('')
  const [resetConfirm, setResetConfirm] = useState('')
  const [resetError, setResetError] = useState('')
  const [resetSuccess, setResetSuccess] = useState(false)
  const [resetSaving, setResetSaving] = useState(false)

  if (!user) return null

  const initials = (user.displayName || user.email)[0].toUpperCase()
  const memberSince = new Date(user.createdAt).toLocaleDateString(undefined, {
    year: 'numeric', month: 'long', day: 'numeric',
  })

  function formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`
  }

  const roleBadge: Record<string, string> = {
    Admin: 'bg-rose-600/20 text-rose-400 border border-rose-500/30',
    User:  'bg-indigo-600/20 text-indigo-400 border border-indigo-500/30',
    Guest: 'bg-slate-600/20 text-slate-400 border border-slate-500/30',
  }

  async function saveName() {
    const trimmed = displayName.trim()
    if (!trimmed) { setNameError('Display name is required.'); return }
    if (trimmed.length > 100) { setNameError('Display name must be 100 characters or fewer.'); return }
    setNameError('')
    setNameSaving(true)
    try {
      await updateProfile(trimmed)
      setEditingName(false)
    } catch (e: unknown) {
      setNameError(e instanceof Error ? e.message : 'Failed to update name.')
    } finally {
      setNameSaving(false)
    }
  }

  function cancelName() {
    setDisplayName(user?.displayName ?? '')
    setNameError('')
    setEditingName(false)
  }

  async function handleReset() {
    setResetError('')
    setResetSuccess(false)
    if (!resetPassword) { setResetError('New password is required.'); return }
    if (resetPassword.length < 8) { setResetError('Password must be at least 8 characters.'); return }
    if (resetPassword !== resetConfirm) { setResetError('Passwords do not match.'); return }
    setResetSaving(true)
    try {
      await authApi.resetOwnPassword(getToken()!, resetPassword)
      setResetSuccess(true)
      setResetPassword('')
      setResetConfirm('')
    } catch (e: unknown) {
      setResetError(e instanceof Error ? e.message : 'Failed to reset password.')
    } finally {
      setResetSaving(false)
    }
  }

  async function savePassword() {
    setPwError('')
    setPwSuccess(false)
    if (!currentPassword) { setPwError('Current password is required.'); return }
    if (!newPassword) { setPwError('New password is required.'); return }
    if (newPassword.length < 8) { setPwError('New password must be at least 8 characters.'); return }
    if (newPassword !== confirmPassword) { setPwError('Passwords do not match.'); return }
    const token = getToken()
    if (!token) return
    setPwSaving(true)
    try {
      await authApi.changePassword(token, currentPassword, newPassword)
      setPwSuccess(true)
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
    } catch (e: unknown) {
      setPwError(e instanceof Error ? e.message : 'Failed to change password.')
    } finally {
      setPwSaving(false)
    }
  }

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 flex items-center gap-5">
        <div className="w-16 h-16 rounded-full bg-indigo-600/30 flex items-center justify-center flex-shrink-0">
          <span className="text-indigo-300 text-2xl font-bold">{initials}</span>
        </div>
        <div className="min-w-0">
          <h1 className="text-white text-xl font-semibold truncate">{user.displayName}</h1>
          <p className="text-slate-400 text-sm truncate">{user.email}</p>
          <span className={`inline-block mt-1.5 text-xs px-2 py-0.5 rounded-full font-medium ${roleBadge[user.role] ?? roleBadge.User}`}>
            {user.role}
          </span>
        </div>
      </div>

      <div className="bg-slate-900 border border-slate-800 rounded-xl divide-y divide-slate-800">
        <div className="p-4 flex items-center gap-3">
          <Mail className="w-4 h-4 text-slate-500 flex-shrink-0" />
          <div className="min-w-0">
            <p className="text-slate-500 text-xs">Email</p>
            <p className="text-white text-sm truncate">{user.email}</p>
          </div>
        </div>
        <div className="p-4 flex items-center gap-3">
          <Shield className="w-4 h-4 text-slate-500 flex-shrink-0" />
          <div>
            <p className="text-slate-500 text-xs">Role</p>
            <p className="text-white text-sm">{user.role}</p>
          </div>
        </div>
        <div className="p-4 flex items-center gap-3">
          <Calendar className="w-4 h-4 text-slate-500 flex-shrink-0" />
          <div>
            <p className="text-slate-500 text-xs">Member since</p>
            <p className="text-white text-sm">{memberSince}</p>
          </div>
        </div>
        <div className="p-4 flex items-center gap-3">
          <HardDrive className="w-4 h-4 text-slate-500 flex-shrink-0" />
          <div>
            <p className="text-slate-500 text-xs">Storage used</p>
            {storage === null
              ? <p className="text-slate-500 text-sm">Loading…</p>
              : <p className="text-white text-sm">
                  {formatBytes(storage.usedBytes)}
                  <span className="text-slate-500 ml-1.5 text-xs">across {storage.documentCount} document{storage.documentCount !== 1 ? 's' : ''}</span>
                </p>
            }
          </div>
        </div>
      </div>

      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <User className="w-4 h-4 text-slate-400" />
            <h2 className="text-white text-sm font-semibold">Display Name</h2>
          </div>
          {!editingName && (
            <button
              onClick={() => setEditingName(true)}
              className="flex items-center gap-1.5 text-xs text-indigo-400 hover:text-indigo-300 transition-colors"
            >
              <Edit2 className="w-3.5 h-3.5" />
              Edit
            </button>
          )}
        </div>

        {editingName ? (
          <div className="space-y-3">
            <input
              type="text"
              value={displayName}
              onChange={e => setDisplayName(e.target.value)}
              maxLength={100}
              className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-indigo-500 transition-colors"
              autoFocus
              onKeyDown={e => { if (e.key === 'Enter') saveName(); if (e.key === 'Escape') cancelName() }}
            />
            {nameError && <p className="text-rose-400 text-xs">{nameError}</p>}
            <div className="flex gap-2">
              <button
                onClick={saveName}
                disabled={nameSaving}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white text-xs rounded-lg transition-colors"
              >
                <Check className="w-3.5 h-3.5" />
                {nameSaving ? 'Saving…' : 'Save'}
              </button>
              <button
                onClick={cancelName}
                disabled={nameSaving}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-slate-700 hover:bg-slate-600 disabled:opacity-50 text-slate-300 text-xs rounded-lg transition-colors"
              >
                <X className="w-3.5 h-3.5" />
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <p className="text-slate-300 text-sm">{user.displayName}</p>
        )}
      </div>

      {user.role === 'Admin' && (
        <div className="bg-slate-900 border border-amber-500/20 rounded-xl p-5 space-y-4">
          <div className="flex items-center gap-2">
            <KeyRound className="w-4 h-4 text-amber-400" />
            <h2 className="text-white text-sm font-semibold">Reset Password</h2>
            <span className="ml-auto text-xs px-2 py-0.5 rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/20">Admin</span>
          </div>
          <p className="text-slate-500 text-xs">Set a new password without entering the current one. Use with care.</p>

          <div className="space-y-3">
            <div>
              <label className="text-slate-500 text-xs block mb-1">New password</label>
              <input
                type="password"
                value={resetPassword}
                onChange={e => setResetPassword(e.target.value)}
                className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-amber-500 transition-colors"
                placeholder="••••••••"
              />
            </div>
            <div>
              <label className="text-slate-500 text-xs block mb-1">Confirm new password</label>
              <input
                type="password"
                value={resetConfirm}
                onChange={e => setResetConfirm(e.target.value)}
                className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-amber-500 transition-colors"
                placeholder="••••••••"
                onKeyDown={e => { if (e.key === 'Enter') handleReset() }}
              />
            </div>

            {resetError && <p className="text-rose-400 text-xs">{resetError}</p>}
            {resetSuccess && <p className="text-emerald-400 text-xs">Password reset successfully.</p>}

            <button
              onClick={handleReset}
              disabled={resetSaving}
              className="px-4 py-2 bg-amber-600 hover:bg-amber-500 disabled:opacity-50 text-white text-xs rounded-lg transition-colors"
            >
              {resetSaving ? 'Resetting…' : 'Reset password'}
            </button>
          </div>
        </div>
      )}

      {!user.isGuest && (
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 space-y-4">
          <div className="flex items-center gap-2">
            <Lock className="w-4 h-4 text-slate-400" />
            <h2 className="text-white text-sm font-semibold">Change Password</h2>
          </div>

          <div className="space-y-3">
            <div>
              <label className="text-slate-500 text-xs block mb-1">Current password</label>
              <input
                type="password"
                value={currentPassword}
                onChange={e => setCurrentPassword(e.target.value)}
                className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-indigo-500 transition-colors"
                placeholder="••••••••"
              />
            </div>
            <div>
              <label className="text-slate-500 text-xs block mb-1">New password</label>
              <input
                type="password"
                value={newPassword}
                onChange={e => setNewPassword(e.target.value)}
                className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-indigo-500 transition-colors"
                placeholder="••••••••"
              />
            </div>
            <div>
              <label className="text-slate-500 text-xs block mb-1">Confirm new password</label>
              <input
                type="password"
                value={confirmPassword}
                onChange={e => setConfirmPassword(e.target.value)}
                className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-indigo-500 transition-colors"
                placeholder="••••••••"
                onKeyDown={e => { if (e.key === 'Enter') savePassword() }}
              />
            </div>

            {pwError && <p className="text-rose-400 text-xs">{pwError}</p>}
            {pwSuccess && <p className="text-emerald-400 text-xs">Password changed successfully.</p>}

            <button
              onClick={savePassword}
              disabled={pwSaving}
              className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white text-xs rounded-lg transition-colors"
            >
              {pwSaving ? 'Updating…' : 'Update password'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
