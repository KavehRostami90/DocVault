import { useState } from 'react'
import { Link } from 'react-router-dom'
import { authApi } from '../api/auth'

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await authApi.forgotPassword(email)
      setSubmitted(true)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.')
    } finally {
      setLoading(false)
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
          {submitted ? (
            <div className="text-center space-y-4">
              <div className="w-12 h-12 bg-indigo-600/20 rounded-full flex items-center justify-center mx-auto">
                <span className="text-indigo-400 text-xl">✓</span>
              </div>
              <h1 className="text-white text-xl font-semibold">Check your inbox</h1>
              <p className="text-slate-400 text-sm">
                If <span className="text-white">{email}</span> is registered, you'll receive a reset link shortly.
              </p>
              <Link
                to="/login"
                className="block w-full text-center bg-slate-800 hover:bg-slate-700 text-slate-300 font-medium rounded-lg py-2.5 text-sm transition-colors border border-slate-700 mt-2"
              >
                Back to sign in
              </Link>
            </div>
          ) : (
            <>
              <h1 className="text-white text-xl font-semibold mb-2">Forgot password</h1>
              <p className="text-slate-400 text-sm mb-6">
                Enter your email and we'll send you a reset link.
              </p>

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
                    autoFocus
                    autoComplete="email"
                    className="w-full bg-slate-800 border border-slate-700 text-white rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
                    placeholder="you@example.com"
                  />
                </div>
                <button
                  type="submit"
                  disabled={loading}
                  className="w-full bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white font-medium rounded-lg py-2.5 text-sm transition-colors"
                >
                  {loading ? 'Sending…' : 'Send reset link'}
                </button>
              </form>
            </>
          )}
        </div>

        {!submitted && (
          <p className="text-center text-slate-500 text-sm mt-6">
            Remember it?{' '}
            <Link to="/login" className="text-indigo-400 hover:text-indigo-300">
              Sign in
            </Link>
          </p>
        )}
      </div>
    </div>
  )
}
