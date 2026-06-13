import { get, post, del } from './client'

export interface ApiKeyDto {
  id: string
  name: string
  keyPrefix: string
  isRevoked: boolean
  expiresAt: string | null
  lastUsedAt: string | null
  createdAt: string
}

export interface CreatedApiKey {
  id: string
  name: string
  rawKey: string
  keyPrefix: string
  expiresAt: string | null
  createdAt: string
}

export const apiKeysApi = {
  list: ()                                        => get<ApiKeyDto[]>('/api-keys'),
  create: (name: string, expiresAt: string | null) => post<CreatedApiKey>('/api-keys', { name, expiresAt }),
  revoke: (id: string)                            => del(`/api-keys/${id}`),
}
