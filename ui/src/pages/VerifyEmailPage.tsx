import { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { authApi } from '../api/auth'

type Status = 'verifying' | 'success' | 'error'

export default function VerifyEmailPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()

  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''

  const [status, setStatus] = useState<Status>('verifying')
  const [resendLoading, setResendLoading] = useState(false)
  const [resendSent, setResendSent] = useState(false)

  useEffect(() => {
    if (!email || !token) { setStatus('error'); return }

    authApi.verifyEmail(email, token)
      .then(() => {
        setStatus('success')
        setTimeout(() => navigate('/login', { state: { emailVerified: true }, replace: true }), 2500)
      })
      .catch(() => setStatus('error'))
  }, []) // run once on mount

  async function handleResend() {
    if (!email || resendSent) return
    setResendLoading(true)
    try {
      await authApi.resendVerification(email)
      setResendSent(true)
    } finally {
      setResendLoading(false)
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

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-8 text-center space-y-4">
          {status === 'verifying' && (
            <>
              <div className="w-12 h-12 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin mx-auto" />
              <h1 className="text-white text-xl font-semibold">Verifying your email…</h1>
            </>
          )}

          {status === 'success' && (
            <>
              <div className="w-12 h-12 bg-emerald-500/20 rounded-full flex items-center justify-center mx-auto">
                <span className="text-emerald-400 text-xl">✓</span>
              </div>
              <h1 className="text-white text-xl font-semibold">Email verified!</h1>
              <p className="text-slate-400 text-sm">Redirecting you to sign in…</p>
              <Link
                to="/login"
                className="block w-full text-center bg-indigo-600 hover:bg-indigo-500 text-white font-medium rounded-lg py-2.5 text-sm transition-colors"
              >
                Sign in now
              </Link>
            </>
          )}

          {status === 'error' && (
            <>
              <div className="w-12 h-12 bg-red-500/20 rounded-full flex items-center justify-center mx-auto">
                <span className="text-red-400 text-xl">✕</span>
              </div>
              <h1 className="text-white text-xl font-semibold">Invalid or expired link</h1>
              <p className="text-slate-400 text-sm">
                This verification link has expired or already been used.
              </p>
              {email && (
                <button
                  onClick={handleResend}
                  disabled={resendLoading || resendSent}
                  className="w-full bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white font-medium rounded-lg py-2.5 text-sm transition-colors"
                >
                  {resendSent ? 'New link sent ✓' : resendLoading ? 'Sending…' : 'Send new verification link'}
                </button>
              )}
              <Link
                to="/login"
                className="block text-sm text-indigo-400 hover:text-indigo-300"
              >
                Back to sign in
              </Link>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
