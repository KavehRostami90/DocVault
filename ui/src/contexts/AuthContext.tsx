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
  updateProfile: (displayName: string) => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

const SESSION_KEY      = 'dv_access_token'
const SESSION_USER_KEY = 'dv_user'

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({
    user: null,
    accessToken: null,
    isLoading: true,
  })

  const refreshTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const isMounted    = useRef(true)

  const scheduleRefresh = useCallback((expiresInSeconds: number) => {
    if (!isMounted.current) return
    if (refreshTimer.current) clearTimeout(refreshTimer.current)
    const delay = Math.max((expiresInSeconds - 60) * 1000, 5000)
    refreshTimer.current = setTimeout(async () => {
      try {
        const res = await authApi.refresh()
        if (!isMounted.current) return
        sessionStorage.setItem(SESSION_KEY, res.accessToken)
        setState(prev => ({ ...prev, accessToken: res.accessToken }))
        scheduleRefresh(res.expiresIn)
      } catch {
        if (!isMounted.current) return
        sessionStorage.removeItem(SESSION_KEY)
        sessionStorage.removeItem(SESSION_USER_KEY)
        setState({ user: null, accessToken: null, isLoading: false })
      }
    }, delay)
  }, [])

  useEffect(() => {
    const storedToken = sessionStorage.getItem(SESSION_KEY)
    const storedUser  = sessionStorage.getItem(SESSION_USER_KEY)

    if (storedToken && storedUser) {
      setState({ user: JSON.parse(storedUser), accessToken: storedToken, isLoading: false })
      authApi.refresh()
        .then(res => {
          sessionStorage.setItem(SESSION_KEY, res.accessToken)
          setState(prev => ({ ...prev, accessToken: res.accessToken }))
          scheduleRefresh(res.expiresIn)
        })
        .catch(() => {
          setState(prev => ({ ...prev, isLoading: false }))
        })
    } else {
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

    return () => {
      isMounted.current = false
      if (refreshTimer.current) clearTimeout(refreshTimer.current)
    }
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
      try { await authApi.logout(token) } catch { }
    }
    if (refreshTimer.current) clearTimeout(refreshTimer.current)
    sessionStorage.removeItem(SESSION_KEY)
    sessionStorage.removeItem(SESSION_USER_KEY)
    setState({ user: null, accessToken: null, isLoading: false })
  }, [])

  const getToken = useCallback(() => state.accessToken, [state.accessToken])

  const updateProfile = useCallback(async (displayName: string) => {
    const token = state.accessToken
    if (!token) throw new Error('Not authenticated')
    await authApi.updateProfile(token, displayName)
    setState(prev => {
      if (!prev.user) return prev
      const updated = { ...prev.user, displayName }
      sessionStorage.setItem(SESSION_USER_KEY, JSON.stringify(updated))
      return { ...prev, user: updated }
    })
  }, [state.accessToken])

  return (
    <AuthContext.Provider value={{ ...state, login, register, loginAsGuest, logout, getToken, updateProfile }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}
