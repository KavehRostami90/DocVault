import { post } from './client'
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
