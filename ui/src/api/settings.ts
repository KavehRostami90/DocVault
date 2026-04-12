import { get } from './client'

export interface UploadSettings {
  maxFileSizeBytes: number
  maxUploadCount: number
}

let uploadSettingsPromise: Promise<UploadSettings> | null = null

export function getUploadSettings(): Promise<UploadSettings> {
  uploadSettingsPromise ??= get<UploadSettings>('/config/upload')
  return uploadSettingsPromise
}
