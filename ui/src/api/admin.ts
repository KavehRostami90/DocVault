import { del, get, post, put } from './client'
import type { DocumentListItem, PageResponse } from '../types'

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

  listDocuments: (page = 1, size = 50) =>
    get<PageResponse<AdminDocument>>(`/admin/documents?page=${page}&size=${size}`),

  deleteDocument: (id: string) => del(`/admin/documents/${id}`),

  reindexDocument: (id: string) =>
    post<void>(`/admin/documents/${id}/reindex`, {}),
}
