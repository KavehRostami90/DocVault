import { del, get, getBlob, post, put } from './client'
import type { DocumentListItem, PageResponse } from '../types'
import type { AdminDocumentFilter } from '../pages/admin/adminFilters'

export interface AdminUser {
  id: string
  email: string
  displayName: string
  isGuest: boolean
  createdAt: string
  roles: string[]
}

export type AdminDocument = DocumentListItem

export interface AdminStats {
  totalUsers: number
  guestUsers: number
  registeredUsers: number
  adminUsers: number
  totalDocuments: number
  documentsByStatus: Record<string, number>
}

export const adminApi = {
  getStats: () => get<AdminStats>('/admin/stats'),

  listUsers: () => get<AdminUser[]>('/admin/users'),

  deleteUser: (id: string) => del(`/admin/users/${id}`),

  updateUserRoles: (id: string, roles: string[]) =>
    put(`/admin/users/${id}/roles`, { roles }),

  listDocuments: (page = 1, size = 50, filter: AdminDocumentFilter = 'all') => {
    const query = new URLSearchParams({
      page: String(page),
      size: String(size),
    })

    if (filter !== 'all')
      query.set('status', filter)

    return get<PageResponse<AdminDocument>>(`/admin/documents?${query}`)
  },

  deleteDocument: (id: string) => del(`/admin/documents/${id}`),

  getDocumentPreviewBlob: (id: string) => getBlob(`/admin/documents/${id}/preview`),

  getDocumentDownloadBlob: (id: string) => getBlob(`/admin/documents/${id}/download`),

  reindexDocument: (id: string) =>
    post<void>(`/admin/documents/${id}/reindex`, {}),
}
