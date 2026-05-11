import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Eye, EyeOff } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import { authApi } from '../api/auth'

export default function LoginPage() {
  const { login, loginAsGuest } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: Location })?.from?.pathname ?? '/documents'

  const state = location.state as {
    resetSuccess?: boolean
    registrationSent?: boolean
    emailVerified?: boolean
    email?: string
  } | null

  const resetSuccess      = state?.resetSuccess
  const registrationSent  = state?.registrationSent
  const emailVerified     = state?.emailVerified
  const registrationEmail = state?.email ?? ''

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [guestLoading, setGuestLoading] = useState(false)
  const [resendLoading, setResendLoading] = useState(false)
  const [resendSent, setResendSent] = useState(false)

  async function handleResend() {
    if (!registrationEmail || resendSent) return
    setResendLoading(true)
    try {
      await authApi.resendVerification(registrationEmail)
      setResendSent(true)
    } finally {
      setResendLoading(false)
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await login(email, password)
      navigate(from, { replace: true })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed.')
    } finally {
      setLoading(false)
    }
  }

  async function handleGuest() {
    setError('')
    setGuestLoading(true)
    try {
      await loginAsGuest()
      navigate('/documents', { replace: true })
    } catch {
      setError('Could not start guest session.')
    } finally {
      setGuestLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-slate-950 flex items-center justify-center p-4">
      <div className="w-full max-w-sm">
        <div className="flex items-center justify-center gap-3 mb-8">
          <div className="w-10 h-10 bg-indigo-600 rounded-xl flex items-center justify-center">
            <span className="text-white font-bold">DV</span>
          </div>
          <span className="text-white font-semibold text-2xl">DocVault</span>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-8">
          <h1 className="text-white text-xl font-semibold mb-6">Sign in</h1>

          {resetSuccess && (
            <div className="bg-emerald-500/10 border border-emerald-500/30 text-emerald-400 text-sm rounded-lg px-4 py-3 mb-4">
              Password reset successfully. Sign in with your new password.
            </div>
          )}

          {emailVerified && (
            <div className="bg-emerald-500/10 border border-emerald-500/30 text-emerald-400 text-sm rounded-lg px-4 py-3 mb-4">
              Email verified! You can now sign in.
            </div>
          )}

          {registrationSent && (
            <div className="bg-indigo-500/10 border border-indigo-500/30 text-indigo-300 text-sm rounded-lg px-4 py-3 mb-4 space-y-2">
              <p>
                We sent a verification link to{' '}
                <span className="text-white font-medium">{registrationEmail}</span>.
                Please check your inbox before signing in.
              </p>
              <button
                onClick={handleResend}
                disabled={resendLoading || resendSent}
                className="text-indigo-400 hover:text-indigo-300 disabled:opacity-50 underline text-xs"
              >
                {resendSent ? 'Verification email resent ✓' : resendLoading ? 'Sending…' : 'Resend verification email'}
              </button>
            </div>
          )}

          {error && (
            <div className="bg-red-500/10 border border-red-500/30 text-red-400 text-sm rounded-lg px-4 py-3 mb-4">
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-sm text-slate-400 mb-1.5">Email</label>
              <input
                type="email"
                value={email}
                onChange={e => setEmail(e.target.value)}
                required
                autoComplete="email"
                className="w-full bg-slate-800 border border-slate-700 text-white rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
                placeholder="you@example.com"
              />
            </div>
            <div>
              <div className="flex items-center justify-between mb-1.5">
                <label className="block text-sm text-slate-400">Password</label>
                <Link to="/forgot-password" className="text-xs text-indigo-400 hover:text-indigo-300">
                  Forgot password?
                </Link>
              </div>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  required
                  autoComplete="current-password"
                  className="w-full bg-slate-800 border border-slate-700 text-white rounded-lg px-3 py-2.5 pr-9 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
                  placeholder="••••••••"
                />
                <button type="button" onClick={() => setShowPassword(v => !v)}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 flex items-center justify-center text-slate-400 hover:text-white transition-colors">
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>
            <button
              type="submit"
              disabled={loading}
              className="w-full bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white font-medium rounded-lg py-2.5 text-sm transition-colors"
            >
              {loading ? 'Signing in…' : 'Sign in'}
            </button>
          </form>

          <div className="flex items-center gap-3 my-5">
            <div className="flex-1 h-px bg-slate-800" />
            <span className="text-slate-600 text-xs">or</span>
            <div className="flex-1 h-px bg-slate-800" />
          </div>

          <button
            onClick={handleGuest}
            disabled={guestLoading}
            className="w-full bg-slate-800 hover:bg-slate-700 disabled:opacity-50 text-slate-300 font-medium rounded-lg py-2.5 text-sm transition-colors border border-slate-700"
          >
            {guestLoading ? 'Starting session…' : 'Try without registering'}
          </button>
          <p className="text-slate-600 text-xs text-center mt-2">Guest sessions are temporary (24h)</p>
        </div>

        <p className="text-center text-slate-500 text-sm mt-6">
          Don't have an account?{' '}
          <Link to="/register" className="text-indigo-400 hover:text-indigo-300">
            Create one
          </Link>
        </p>
      </div>
    </div>
  )
}
