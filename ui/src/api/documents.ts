import { get, put, del, upload } from './client'
import type { DocumentListItem, DocumentDetail, PageResponse } from '../types'

export interface ListParams {
  page?: number
  size?: number
  title?: string
  status?: string
  tag?: string
  sort?: string
  desc?: boolean
}

export function listDocuments(p: ListParams = {}): Promise<PageResponse<DocumentListItem>> {
  const q = new URLSearchParams()
  if (p.page) q.set('page', String(p.page))
  if (p.size) q.set('size', String(p.size))
  if (p.title) q.set('title', p.title)
  if (p.status) q.set('status', p.status)
  if (p.tag) q.set('tag', p.tag)
  if (p.sort) q.set('sort', p.sort)
  if (p.desc) q.set('desc', 'true')
  return get(`/documents?${q}`)
}

export function getDocument(id: string): Promise<DocumentDetail> {
  return get(`/documents/${id}`)
}

export function uploadDocument(title: string, tags: string[], file: File): Promise<{ id: string }> {
  const form = new FormData()
  form.append('title', title)
  form.append('file', file)
  tags.forEach(t => form.append('tags', t))
  return upload('/documents', form)
}

export function updateTags(id: string, tags: string[]): Promise<void> {
  return put(`/documents/${id}/tags`, { tags })
}

export function deleteDocument(id: string): Promise<void> {
  return del(`/documents/${id}`)
}
