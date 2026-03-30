import { post } from './client'
import type { PageResponse, SearchResultItem } from '../types'

export function searchDocuments(query: string, page = 1, size = 20): Promise<PageResponse<SearchResultItem>> {
  return post('/search/documents', { query, page, size })
}
