export type DocumentStatus = 'Imported' | 'Indexed' | 'Failed'

export interface DocumentListItem {
  id: string
  title: string
  fileName: string
  status: DocumentStatus
  size: number
  createdAt: string
  ownerId: string | null
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

export interface QaCitation {
  documentId: string
  title: string
  excerpt: string
  score: number
}

export interface QaResponse {
  answer: string
  answeredByModel: boolean
  citations: QaCitation[]
}

export interface ImportStatus {
  id: string
  fileName: string
  status: 'Pending' | 'InProgress' | 'Completed' | 'Failed'
  startedAt: string
  completedAt: string | null
  error: string | null
}
