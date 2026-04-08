import { useState } from 'react'
import { BarChart2, FileText, Shield, Users } from 'lucide-react'
import AdminOverviewTab from './AdminOverviewTab'
import AdminUsersTab from './AdminUsersTab'
import AdminDocumentsTab from './AdminDocumentsTab'

type Tab = 'overview' | 'users' | 'documents'

const TABS: { id: Tab; label: string; icon: React.ReactNode }[] = [
  { id: 'overview',   label: 'Overview',   icon: <BarChart2 className="w-4 h-4" /> },
  { id: 'users',      label: 'Users',      icon: <Users className="w-4 h-4" /> },
  { id: 'documents',  label: 'Documents',  icon: <FileText className="w-4 h-4" /> },
]

export default function AdminDashboardPage() {
  const [tab, setTab] = useState<Tab>('overview')

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Shield className="w-6 h-6 text-indigo-400" />
        <h1 className="text-white text-2xl font-semibold">Admin Dashboard</h1>
      </div>

      <div className="flex gap-1 bg-slate-900 rounded-lg p-1 w-fit border border-slate-800">
        {TABS.map(t => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`flex items-center gap-2 px-4 py-1.5 rounded-md text-sm font-medium transition-colors ${
              tab === t.id ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-white'
            }`}
          >
            {t.icon}
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'overview'  && <AdminOverviewTab />}
      {tab === 'users'     && <AdminUsersTab />}
      {tab === 'documents' && <AdminDocumentsTab />}
    </div>
  )
}

