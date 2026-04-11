import type { DocumentStatus } from '../../types'

export type AdminUserFilter = 'all' | 'registered' | 'guests' | 'admins'

export type AdminDocumentFilter = 'all' | DocumentStatus
