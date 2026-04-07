import { get } from './client'

export function listTags(): Promise<{ name: string }[]> {
  return get('/tags')
}
