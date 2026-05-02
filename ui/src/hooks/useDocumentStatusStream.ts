import { useEffect, useRef } from 'react'
import { authApi } from '../api/auth'
import type { DocumentStatus } from '../types'

const BASE = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? ''

interface DocumentStatusSseEvent {
  documentId: string
  status: DocumentStatus
  error?: string | null
}

async function openStatusStream(
  documentId: string,
  onEvent: (status: DocumentStatus, error?: string | null) => void,
  signal: AbortSignal,
  retried = false,
): Promise<void> {
  const token = sessionStorage.getItem('dv_access_token')

  let response: Response
  try {
    response = await fetch(`${BASE}/api/v1/documents/${documentId}/status-stream`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      credentials: 'include',
      signal,
    })
  } catch (e) {
    if ((e as Error).name === 'AbortError') return
    return
  }

  if (response.status === 401 && !retried) {
    try {
      const result = await authApi.refresh()
      sessionStorage.setItem('dv_access_token', result.accessToken)
      return openStatusStream(documentId, onEvent, signal, true)
    } catch {
      return
    }
  }

  if (!response.ok || !response.body) return

  const reader  = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer    = ''

  try {
    while (true) {
      const { done, value } = await reader.read()
      if (done) break

      buffer += decoder.decode(value, { stream: true })
      const blocks = buffer.split('\n\n')
      buffer = blocks.pop() ?? ''

      for (const block of blocks) {
        for (const line of block.split('\n')) {
          if (line.startsWith('data: ')) {
            try {
              const evt: DocumentStatusSseEvent = JSON.parse(line.slice(6))
              onEvent(evt.status, evt.error)
            } catch { }
          }
        }
      }
    }
  } catch (e) {
    if ((e as Error).name === 'AbortError') return
  } finally {
    reader.cancel().catch(() => {})
  }
}

/** SSE hook for `/documents/{id}/status-stream`. Closes when a terminal status arrives or `active` goes false. */
export function useDocumentStatusStream(
  documentId: string | undefined,
  active: boolean,
  onStatusChange: (status: DocumentStatus, error?: string | null) => void,
) {
  const onStatusChangeRef = useRef(onStatusChange)
  onStatusChangeRef.current = onStatusChange

  useEffect(() => {
    if (!documentId || !active) return

    const controller = new AbortController()

    openStatusStream(
      documentId,
      (status, error) => onStatusChangeRef.current(status, error),
      controller.signal,
    )

    return () => controller.abort()
  }, [documentId, active])
}
