import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { FileText, Search, Menu, X, Shield, LogOut } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '../contexts/AuthContext'
import { initClient } from '../api/client'

export default function Layout() {
  const [open, setOpen] = useState(false)
  const { user, logout, getToken } = useAuth()
  const navigate = useNavigate()

  // Wire up the API client with the current token and the 401 handler
  initClient(getToken, () => navigate('/login', { replace: true }))

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
  }

  const navLinks = [
    { to: '/documents', icon: FileText, label: 'Documents' },
    { to: '/search', icon: Search, label: 'Search' },
    ...(user?.role === 'Admin' ? [{ to: '/admin', icon: Shield, label: 'Admin' }] : []),
  ]

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
          {navLinks.map(({ to, icon: Icon, label }) => (
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

        <div className="p-4 border-t border-slate-800 space-y-3">
          {user && (
            <NavLink
              to="/profile"
              onClick={() => setOpen(false)}
              className="flex items-center gap-2 rounded-lg px-1 py-1 hover:bg-slate-800 transition-colors group"
            >
              <div className="w-7 h-7 rounded-full bg-indigo-600/30 flex items-center justify-center flex-shrink-0">
                <span className="text-indigo-300 text-xs font-semibold uppercase">
                  {(user.displayName || user.email)[0]}
                </span>
              </div>
              <div className="min-w-0">
                <p className="text-white text-xs font-medium truncate group-hover:text-indigo-300 transition-colors">
                  {user.displayName || user.email}
                </p>
                <p className="text-slate-500 text-xs truncate">
                  {user.isGuest ? 'Guest session' : user.role}
                </p>
              </div>
            </NavLink>
          )}
          {user?.isGuest && (
            <NavLink
              to="/register"
              className="block w-full text-center text-xs bg-indigo-600/20 hover:bg-indigo-600/30 text-indigo-400 rounded-lg py-1.5 transition-colors"
            >
              Save your work — Register
            </NavLink>
          )}
          <button
            onClick={handleLogout}
            className="flex items-center gap-2 w-full text-slate-400 hover:text-white text-xs px-1 transition-colors"
          >
            <LogOut className="w-3.5 h-3.5" />
            Sign out
          </button>
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
