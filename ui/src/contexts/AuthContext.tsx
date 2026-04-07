import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import { authApi, type UserInfo } from '../api/auth'

interface AuthState {
  user: UserInfo | null
  accessToken: string | null
  isLoading: boolean
}

interface AuthContextValue extends AuthState {
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, displayName: string) => Promise<void>
  loginAsGuest: () => Promise<void>
  logout: () => Promise<void>
  getToken: () => string | null
}

const AuthContext = createContext<AuthContextValue | null>(null)

const SESSION_KEY = 'dv_access_token'
const SESSION_USER_KEY = 'dv_user'

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({
    user: null,
    accessToken: null,
    isLoading: true,
  })

  const refreshTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const scheduleRefresh = useCallback((expiresInSeconds: number) => {
    if (refreshTimer.current) clearTimeout(refreshTimer.current)
    // Refresh 60 seconds before expiry
    const delay = Math.max((expiresInSeconds - 60) * 1000, 5000)
    refreshTimer.current = setTimeout(async () => {
      try {
        const res = await authApi.refresh()
        sessionStorage.setItem(SESSION_KEY, res.accessToken)
        setState(prev => ({ ...prev, accessToken: res.accessToken }))
        scheduleRefresh(res.expiresIn)
      } catch {
        // Refresh failed — clear session
        sessionStorage.removeItem(SESSION_KEY)
        sessionStorage.removeItem(SESSION_USER_KEY)
        setState({ user: null, accessToken: null, isLoading: false })
      }
    }, delay)
  }, [])

  // On mount: restore token from sessionStorage then try a silent refresh to verify it's still valid
  useEffect(() => {
    const storedToken = sessionStorage.getItem(SESSION_KEY)
    const storedUser = sessionStorage.getItem(SESSION_USER_KEY)

    if (storedToken && storedUser) {
      setState({ user: JSON.parse(storedUser), accessToken: storedToken, isLoading: false })
      // Silently refresh to extend session
      authApi.refresh()
        .then(res => {
          sessionStorage.setItem(SESSION_KEY, res.accessToken)
          setState(prev => ({ ...prev, accessToken: res.accessToken }))
          scheduleRefresh(res.expiresIn)
        })
        .catch(() => {
          // Cookie expired — keep using stored token until it expires
          setState(prev => ({ ...prev, isLoading: false }))
        })
    } else {
      // Try silent refresh in case cookie is still valid (e.g. new tab)
      authApi.refresh()
        .then(async res => {
          const user = await authApi.me(res.accessToken)
          sessionStorage.setItem(SESSION_KEY, res.accessToken)
          sessionStorage.setItem(SESSION_USER_KEY, JSON.stringify(user))
          setState({ user, accessToken: res.accessToken, isLoading: false })
          scheduleRefresh(res.expiresIn)
        })
        .catch(() => setState({ user: null, accessToken: null, isLoading: false }))
    }

    return () => { if (refreshTimer.current) clearTimeout(refreshTimer.current) }
  }, [scheduleRefresh])

  const setAuth = useCallback((user: UserInfo, accessToken: string, expiresIn: number) => {
    sessionStorage.setItem(SESSION_KEY, accessToken)
    sessionStorage.setItem(SESSION_USER_KEY, JSON.stringify(user))
    setState({ user, accessToken, isLoading: false })
    scheduleRefresh(expiresIn)
  }, [scheduleRefresh])

  const login = useCallback(async (email: string, password: string) => {
    const res = await authApi.login(email, password)
    setAuth(res.user, res.accessToken, res.expiresIn)
  }, [setAuth])

  const register = useCallback(async (email: string, password: string, displayName: string) => {
    const res = await authApi.register(email, password, displayName)
    setAuth(res.user, res.accessToken, res.expiresIn)
  }, [setAuth])

  const loginAsGuest = useCallback(async () => {
    const res = await authApi.guest()
    setAuth(res.user, res.accessToken, res.expiresIn)
  }, [setAuth])

  const logout = useCallback(async () => {
    const token = sessionStorage.getItem(SESSION_KEY)
    if (token) {
      try { await authApi.logout(token) } catch { /* ignore */ }
    }
    if (refreshTimer.current) clearTimeout(refreshTimer.current)
    sessionStorage.removeItem(SESSION_KEY)
    sessionStorage.removeItem(SESSION_USER_KEY)
    setState({ user: null, accessToken: null, isLoading: false })
  }, [])

  const getToken = useCallback(() => state.accessToken, [state.accessToken])

  return (
    <AuthContext.Provider value={{ ...state, login, register, loginAsGuest, logout, getToken }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}
