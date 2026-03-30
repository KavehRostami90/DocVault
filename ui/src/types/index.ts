export type DocumentStatus = 'Pending' | 'Imported' | 'Indexed' | 'Failed'

export interface DocumentListItem {
  id: string
  title: string
  fileName: string
  status: DocumentStatus
}

export interface DocumentDetail {
  id: string
  title: string
  fileName: string
  contentType: string
  size: number
  status: DocumentStatus
  tags: string[]
}

export interface PageResponse<T> {
  items: T[]
  page: number
  size: number
  totalCount: number
}

export interface SearchResultItem {
  id: string
  title: string
  snippet: string
  score: number
}

export interface ImportStatus {
  id: string
  fileName: string
  status: string
  startedAt: string
  completedAt: string | null
  error: string | null
}
