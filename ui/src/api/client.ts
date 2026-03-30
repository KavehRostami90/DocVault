const BASE = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? ''

export async function get<T>(path: string): Promise<T> {
  const r = await fetch(`${BASE}/api/v1${path}`)
  if (!r.ok) throw new Error(`${r.status} ${r.statusText}`)
  return r.json() as Promise<T>
}

export async function post<T>(path: string, body: unknown): Promise<T> {
  const r = await fetch(`${BASE}/api/v1${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!r.ok) throw new Error(`${r.status} ${r.statusText}`)
  return r.json() as Promise<T>
}

export async function put(path: string, body: unknown): Promise<void> {
  const r = await fetch(`${BASE}/api/v1${path}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!r.ok) throw new Error(`${r.status} ${r.statusText}`)
}

export async function del(path: string): Promise<void> {
  const r = await fetch(`${BASE}/api/v1${path}`, { method: 'DELETE' })
  if (!r.ok) throw new Error(`${r.status} ${r.statusText}`)
}

export async function upload<T>(path: string, form: FormData): Promise<T> {
  const r = await fetch(`${BASE}/api/v1${path}`, { method: 'POST', body: form })
  if (!r.ok) throw new Error(`${r.status} ${r.statusText}`)
  return r.json() as Promise<T>
}
