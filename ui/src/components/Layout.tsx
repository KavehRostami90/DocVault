import { Outlet, NavLink } from 'react-router-dom'
import { FileText, Search, Menu, X } from 'lucide-react'
import { useState } from 'react'

export default function Layout() {
  const [open, setOpen] = useState(false)

  return (
    <div className="min-h-screen bg-slate-950 flex">
      <aside className={`fixed inset-y-0 left-0 z-40 w-60 bg-slate-900 border-r border-slate-800 flex flex-col transform transition-transform duration-200 ${open ? 'translate-x-0' : '-translate-x-full'} lg:translate-x-0`}>
        <div className="h-16 flex items-center gap-3 px-6 border-b border-slate-800">
          <div className="w-8 h-8 bg-indigo-600 rounded-lg flex items-center justify-center">
            <span className="text-white font-bold text-sm">DV</span>
          </div>
          <span className="text-white font-semibold text-lg">DocVault</span>
        </div>
        <nav className="flex-1 p-4 space-y-1">
          {[
            { to: '/documents', icon: FileText, label: 'Documents' },
            { to: '/search', icon: Search, label: 'Search' },
          ].map(({ to, icon: Icon, label }) => (
            <NavLink
              key={to}
              to={to}
              onClick={() => setOpen(false)}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                  isActive ? 'bg-indigo-600/20 text-indigo-400' : 'text-slate-400 hover:text-white hover:bg-slate-800'
                }`
              }
            >
              <Icon className="w-4 h-4" />
              {label}
            </NavLink>
          ))}
        </nav>
        <div className="p-4 border-t border-slate-800">
          <p className="text-xs text-slate-600 text-center">DocVault v1</p>
        </div>
      </aside>

      {open && <div className="fixed inset-0 z-30 bg-black/60 lg:hidden" onClick={() => setOpen(false)} />}

      <div className="flex-1 flex flex-col lg:pl-60 min-h-screen">
        <header className="h-16 flex items-center justify-between px-4 border-b border-slate-800 lg:hidden bg-slate-900">
          <div className="flex items-center gap-3">
            <div className="w-7 h-7 bg-indigo-600 rounded-lg flex items-center justify-center">
              <span className="text-white font-bold text-xs">DV</span>
            </div>
            <span className="text-white font-semibold">DocVault</span>
          </div>
          <button onClick={() => setOpen(!open)} className="text-slate-400 hover:text-white">
            {open ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
          </button>
        </header>
        <main className="flex-1 p-6 overflow-y-auto">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
