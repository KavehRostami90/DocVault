import { authApi } from './auth'
import { parseApiError } from './errors'

const BASE = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? ''

// Injected by AuthProvider — avoids a circular dep on the context module.
let _getToken: (() => string | null) | null = null
let _onUnauthorized: (() => void) | null = null

export function initClient(getToken: () => string | null, onUnauthorized: () => void) {
  _getToken = getToken
  _onUnauthorized = onUnauthorized
}

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

/** POST a JSON body and stream SSE back. Calls `onToken` per delta; resolves on `[DONE]`. */
export async function postStream(
  path: string,
  body: unknown,
  onToken: (token: string) => void,
  signal?: AbortSignal,
): Promise<void> {
  const r = await send(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
    body: JSON.stringify(body),
    signal,
  })

  const reader  = r.body!.getReader()
  const decoder = new TextDecoder()
  let buffer    = ''

  // eslint-disable-next-line no-constant-condition
  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    buffer += decoder.decode(value, { stream: true })
    const lines = buffer.split('\n')
    buffer = lines.pop() ?? ''

    for (const line of lines) {
      if (!line.startsWith('data: ')) continue
      const data = line.slice('data: '.length).trim()
      if (data === '[DONE]') return
      try {
        const token: string = JSON.parse(data)
        onToken(token)
      } catch { }
    }
  }
}
