import { parseApiError } from './errors'

const BASE = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? ''

export interface UserInfo {
  id: string
  email: string
  displayName: string
  role: 'Admin' | 'User' | 'Guest'
  isGuest: boolean
}

export interface AuthResponse {
  accessToken: string
  expiresIn: number
  user: UserInfo
}

export interface RefreshResponse {
  accessToken: string
  expiresIn: number
}

async function call<T>(path: string, init: RequestInit): Promise<T> {
  const r = await fetch(`${BASE}/api/v1${path}`, {
    ...init,
    credentials: 'include', // send httpOnly refresh-token cookie
    headers: { 'Content-Type': 'application/json', ...init.headers },
  })
  if (!r.ok) throw new Error(await parseApiError(r))
  if (r.status === 204) return undefined as unknown as T
  return r.json()
}

export const authApi = {
  register: (email: string, password: string, displayName: string) =>
    call<AuthResponse>('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password, displayName }),
    }),

  login: (email: string, password: string) =>
    call<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  guest: () =>
    call<AuthResponse>('/auth/guest', { method: 'POST', body: '{}' }),

  refresh: () =>
    call<RefreshResponse>('/auth/refresh', { method: 'POST', body: '{}' }),

  logout: (token: string) =>
    call<void>('/auth/logout', {
      method: 'POST',
      body: '{}',
      headers: { Authorization: `Bearer ${token}` },
    }),

  me: (token: string) =>
    call<UserInfo>('/auth/me', {
      method: 'GET',
      headers: { Authorization: `Bearer ${token}` },
    }),
}
