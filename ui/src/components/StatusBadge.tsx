import type { DocumentStatus } from '../types'

const config: Record<DocumentStatus, { label: string; className: string }> = {
  Imported: { label: 'Imported', className: 'bg-sky-400/10 text-sky-400 ring-sky-400/20' },
  Indexed:  { label: 'Indexed',  className: 'bg-emerald-400/10 text-emerald-400 ring-emerald-400/20' },
  Failed:   { label: 'Failed',   className: 'bg-red-400/10 text-red-400 ring-red-400/20' },
}

export default function StatusBadge({ status }: { status: string }) {
  const c = config[status as DocumentStatus] ?? { label: status, className: 'bg-slate-700 text-slate-300 ring-slate-700' }
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${c.className}`}>
      {c.label}
    </span>
  )
}
