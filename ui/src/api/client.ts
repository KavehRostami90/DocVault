import { authApi } from './auth'
import { parseApiError } from './errors'

const BASE = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? ''

// Token accessor injected by AuthProvider — avoids a circular dep on the context
let _getToken: (() => string | null) | null = null
let _onUnauthorized: (() => void) | null = null

export function initClient(getToken: () => string | null, onUnauthorized: () => void) {
  _getToken = getToken
  _onUnauthorized = onUnauthorized
}

// Singleton in-flight refresh promise. Concurrent 401s share one refresh call;
// the winner's result is applied to all waiters.
let _refreshPromise: Promise<string> | null = null

async function refreshOnce(): Promise<string> {
  if (!_refreshPromise) {
    _refreshPromise = authApi.refresh()
      .then(res => {
        sessionStorage.setItem('dv_access_token', res.accessToken)
        return res.accessToken
      })
      .finally(() => { _refreshPromise = null })
  }
  return _refreshPromise
}

async function send(path: string, init: RequestInit = {}): Promise<Response> {
  const token = _getToken?.()

  const headers: Record<string, string> = {
    ...(init.headers as Record<string, string>),
  }
  if (token) headers['Authorization'] = `Bearer ${token}`

  const doFetch = () =>
    fetch(`${BASE}/api/v1${path}`, { ...init, headers, credentials: 'include' })

  let r = await doFetch()

  // On 401: attempt one silent token refresh then retry.
  // refreshOnce() serialises concurrent calls so only one refresh hits the server.
  if (r.status === 401) {
    try {
      const newToken = await refreshOnce()
      headers['Authorization'] = `Bearer ${newToken}`
      r = await doFetch()
    } catch {
      _onUnauthorized?.()
      throw new Error('Session expired. Please log in again.')
    }
  }

  if (!r.ok) throw new Error(await parseApiError(r))
  return r
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const r = await send(path, init)
  if (r.status === 204) return undefined as unknown as T
  return r.json() as Promise<T>
}

export async function getText(path: string): Promise<string> {
  const r = await send(path, { method: 'GET' })
  if (r.status === 204) return ''
  return r.text()
}

export async function get<T>(path: string): Promise<T> {
  return request<T>(path, { method: 'GET' })
}

export async function getBlob(path: string): Promise<Blob> {
  const response = await send(path, { method: 'GET' })
  return response.blob()
}

export async function post<T>(path: string, body: unknown): Promise<T> {
  return request<T>(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}

export async function put(path: string, body: unknown): Promise<void> {
  return request<void>(path, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}

export async function del(path: string): Promise<void> {
  return request<void>(path, { method: 'DELETE' })
}

export async function upload<T>(path: string, form: FormData): Promise<T> {
  return request<T>(path, { method: 'POST', body: form })
}
