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
    return // network error — silently give up
  }

  if (response.status === 401 && !retried) {
    try {
      const result = await authApi.refresh()
      sessionStorage.setItem('dv_access_token', result.accessToken)
      return openStatusStream(documentId, onEvent, signal, true)
    } catch {
      return // auth failed — silently give up
    }
  }

  if (!response.ok || !response.body) return

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  try {
    while (true) {
      const { done, value } = await reader.read()
      if (done) break

      buffer += decoder.decode(value, { stream: true })
      // SSE messages are separated by double newlines.
      const blocks = buffer.split('\n\n')
      buffer = blocks.pop() ?? ''

      for (const block of blocks) {
        for (const line of block.split('\n')) {
          if (line.startsWith('data: ')) {
            try {
              const evt: DocumentStatusSseEvent = JSON.parse(line.slice(6))
              onEvent(evt.status, evt.error)
            } catch {
              // ignore parse errors
            }
          }
        }
      }
    }
  } catch (e) {
    if ((e as Error).name !== 'AbortError') {
      // silently ignore stream read errors
    }
  } finally {
    reader.cancel().catch(() => {})
  }
}

/**
 * Opens a fetch-based SSE connection to `/documents/{id}/status-stream` and
 * calls `onStatusChange` whenever the document transitions to a new status.
 * The stream is automatically closed once a terminal status (Indexed / Failed)
 * is received, or when `active` becomes false / the component unmounts.
 *
 * Uses fetch instead of EventSource so the Authorization header can be sent.
 */
export function useDocumentStatusStream(
  documentId: string | undefined,
  active: boolean,
  onStatusChange: (status: DocumentStatus, error?: string | null) => void,
) {
  // Keep a stable ref to avoid restarting the stream on every render.
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
