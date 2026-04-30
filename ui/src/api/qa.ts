import { post, postStream } from './client'
import type { QaResponse } from '../types'

export interface AskQuestionPayload {
  question: string
  maxDocuments?: number
  maxContexts?: number
  documentId?: string
}

export function askQuestion(payload: AskQuestionPayload): Promise<QaResponse> {
  return post('/qa/ask', payload)
}

/**
 * Streaming variant — calls onToken for each LLM token delta as it arrives.
 * Resolves when the stream is complete. Pass an AbortSignal to cancel.
 */
export function askQuestionStream(
  payload: AskQuestionPayload,
  onToken: (token: string) => void,
  signal?: AbortSignal,
): Promise<void> {
  return postStream('/qa/ask/stream', payload, onToken, signal)
}
