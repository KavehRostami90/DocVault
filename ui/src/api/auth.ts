import { parseApiError } from './errors'

const BASE = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? ''

export interface UserInfo {
  id: string
  email: string
  displayName: string
  role: 'Admin' | 'User' | 'Guest'
  isGuest: boolean
  createdAt: string
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
    credentials: 'include',
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

  updateProfile: (token: string, displayName: string) =>
    call<void>('/auth/me', {
      method: 'PUT',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ displayName }),
    }),

  changePassword: (token: string, currentPassword: string, newPassword: string) =>
    call<void>('/auth/me/password', {
      method: 'PUT',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ currentPassword, newPassword }),
    }),

  resetOwnPassword: (token: string, newPassword: string) =>
    call<void>('/auth/me/reset-password', {
      method: 'PUT',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ newPassword }),
    }),

  forgotPassword: (email: string) =>
    call<{ message: string }>('/auth/forgot-password', {
      method: 'POST',
      body: JSON.stringify({ email }),
    }),

  resetPassword: (email: string, token: string, newPassword: string) =>
    call<void>('/auth/reset-password', {
      method: 'POST',
      body: JSON.stringify({ email, token, newPassword }),
    }),
}
