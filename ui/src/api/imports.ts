import { get } from './client'
import type { ImportStatus } from '../types'

export function getImportStatus(id: string): Promise<ImportStatus> {
  return get(`/imports/${id}`)
}
