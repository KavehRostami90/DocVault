import { useEffect, useState } from 'react'
import { BarChart2, FileText, Users } from 'lucide-react'
import { adminApi, type AdminStats } from '../../api/admin'

export default function AdminOverviewTab() {
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    adminApi.getStats()
      .then(setStats)
      .catch(() => setError('Failed to load stats.'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) {
    return <p className="text-slate-500 text-sm">Loading stats…</p>
  }

  if (error) {
    return (
      <div className="bg-red-500/10 border border-red-500/30 text-red-400 text-sm rounded-lg px-4 py-3">
        {error}
      </div>
    )
  }

  const s = stats!

  return (
    <div className="space-y-6">
      <h2 className="text-slate-300 font-medium flex items-center gap-2">
        <BarChart2 className="w-4 h-4" /> Overview
      </h2>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard label="Total Users" value={s.totalUsers} icon={<Users className="w-5 h-5" />} />
        <StatCard label="Registered" value={s.registeredUsers} icon={<Users className="w-5 h-5" />} colour="indigo" />
        <StatCard label="Guests" value={s.guestUsers} icon={<Users className="w-5 h-5" />} colour="amber" />
        <StatCard label="Admins" value={s.adminUsers} icon={<Users className="w-5 h-5" />} colour="purple" />
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard label="Total Documents" value={s.totalDocuments} icon={<FileText className="w-5 h-5" />} />
        {Object.entries(s.documentsByStatus).map(([status, count]) => (
          <StatCard key={status} label={status} value={count} icon={<FileText className="w-5 h-5" />} colour={statusColor(status)} />
        ))}
      </div>
    </div>
  )
}

function statusColor(status: string): string {
  switch (status.toLowerCase()) {
    case 'indexed':  return 'emerald'
    case 'failed':   return 'red'
    case 'imported': return 'sky'
    default:         return 'slate'
  }
}

function StatCard({ label, value, icon, colour = 'slate' }: { label: string; value: number | string; icon: React.ReactNode; colour?: string }) {
  const accent: Record<string, string> = {
    slate:   'text-slate-400',
    indigo:  'text-indigo-400',
    amber:   'text-amber-400',
    purple:  'text-purple-400',
    emerald: 'text-emerald-400',
    red:     'text-red-400',
    sky:     'text-sky-400',
  }
  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-4">
      <div className={`flex items-center gap-2 mb-2 ${accent[colour] ?? accent.slate}`}>
        {icon}
        <span className="text-xs font-medium uppercase tracking-wide">{label}</span>
      </div>
      <div className="text-white text-2xl font-bold">{value}</div>
    </div>
  )
}
