import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, FileText, Zap } from 'lucide-react'
import { searchDocuments } from '../api/search'
import type { SearchResultItem } from '../types'

export default function SearchPage() {
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<SearchResultItem[]>([])
  const [searched, setSearched] = useState(false)
  const [loading, setLoading] = useState(false)

  const search = async () => {
    if (!query.trim()) return
    setLoading(true)
    try {
      const res = await searchDocuments(query.trim())
      setResults(res.items)
      setSearched(true)
    } catch {
      setResults([])
      setSearched(true)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="max-w-3xl mx-auto">
      <div className="mb-10 text-center">
        <div className="inline-flex items-center justify-center w-12 h-12 bg-indigo-600/20 rounded-2xl mb-4">
          <Zap className="w-6 h-6 text-indigo-400" />
        </div>
        <h1 className="text-3xl font-bold text-white mb-2">Semantic Search</h1>
        <p className="text-slate-400">Find documents using natural language</p>
      </div>

      <div className="relative mb-8">
        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-500" />
        <input
          value={query}
          onChange={e => setQuery(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && search()}
          placeholder="Search your documents..."
          className="w-full bg-slate-900 border border-slate-700 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 rounded-xl pl-12 pr-28 py-4 text-white placeholder-slate-500 text-base outline-none transition-colors"
        />
        <button
          onClick={search}
          disabled={!query.trim() || loading}
          className="absolute right-2 top-1/2 -translate-y-1/2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed text-white px-5 py-2 rounded-lg text-sm font-medium transition-colors"
        >
          {loading ? <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin block" /> : 'Search'}
        </button>
      </div>

      {searched && results.length === 0 && (
        <div className="text-center py-16">
          <p className="text-slate-400">No results for <span className="text-white">"{query}"</span></p>
          <p className="text-slate-600 text-sm mt-1">Try different keywords or check if documents are indexed</p>
        </div>
      )}

      {results.length > 0 && (
        <div className="space-y-3">
          <p className="text-slate-500 text-sm">{results.length} result{results.length !== 1 ? 's' : ''}</p>
          {results.map(r => (
            <button key={r.id} onClick={() => navigate(`/documents/${r.id}`)} className="w-full bg-slate-900 hover:bg-slate-800 border border-slate-800 hover:border-slate-700 rounded-xl p-5 text-left transition-all group">
              <div className="flex items-start gap-4">
                <div className="w-9 h-9 bg-indigo-600/10 rounded-lg flex items-center justify-center shrink-0 group-hover:bg-indigo-600/20 transition-colors">
                  <FileText className="w-4 h-4 text-indigo-400" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between gap-4 mb-1">
                    <p className="text-white font-medium text-sm">{r.title}</p>
                    <span className="text-xs text-slate-500 shrink-0">Score: {(r.score * 100).toFixed(0)}%</span>
                  </div>
                  <p className="text-slate-400 text-xs line-clamp-2">{r.snippet}</p>
                </div>
              </div>
            </button>
          ))}
        </div>
      )}

      {!searched && (
        <div className="text-center py-16 text-slate-600">
          <p className="text-sm">Press Enter or click Search to find documents</p>
        </div>
      )}
    </div>
  )
}
