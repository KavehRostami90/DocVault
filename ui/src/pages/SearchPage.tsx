import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, FileText, Zap, AlertCircle, Sparkles } from 'lucide-react'
import { searchDocuments } from '../api/search'
import { askQuestion } from '../api/qa'
import type { QaResponse, SearchResultItem } from '../types'

export default function SearchPage() {
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<SearchResultItem[]>([]
  )
  const [searched, setSearched] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [answering, setAnswering] = useState(false)
  const [qa, setQa] = useState<QaResponse | null>(null)
  const [qaError, setQaError] = useState<string | null>(null)

  const search = async () => {
    if (!query.trim()) return
    setLoading(true)
    setError(null)
    try {
      const res = await searchDocuments(query.trim())
      setResults(res.items)
      setSearched(true)
      setQa(null)
      setQaError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Search failed')
      setSearched(true)
    } finally {
      setLoading(false)
    }
  }

  const ask = async () => {
    if (!query.trim()) return
    setAnswering(true)
    setQaError(null)
    try {
      const res = await askQuestion({ question: query.trim(), maxDocuments: 8, maxContexts: 6 })
      setQa(res)
    } catch (e) {
      setQaError(e instanceof Error ? e.message : 'Question answering failed')
      setQa(null)
    } finally {
      setAnswering(false)
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
        <button
          onClick={ask}
          disabled={!query.trim() || answering}
          className="absolute right-28 top-1/2 -translate-y-1/2 bg-slate-700 hover:bg-slate-600 disabled:opacity-50 disabled:cursor-not-allowed text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors"
        >
          {answering ? <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin block" /> : 'Ask'}
        </button>
      </div>

      {qaError && (
        <div className="flex items-center gap-3 bg-red-900/20 border border-red-900/30 rounded-xl p-4 mb-6">
          <AlertCircle className="w-5 h-5 text-red-400 shrink-0" />
          <p className="text-red-400 text-sm">{qaError}</p>
        </div>
      )}

      {qa && (
        <div className="bg-slate-900 border border-indigo-900/40 rounded-xl p-5 mb-6">
          <div className="flex items-center gap-2 mb-2">
            <Sparkles className="w-4 h-4 text-indigo-400" />
            <p className="text-white text-sm font-medium">Answer</p>
            <span className="text-xs text-slate-500">
              {qa.answeredByModel ? 'Model-generated with citations' : 'Extractive fallback'}
            </span>
          </div>
          <p className="text-slate-200 text-sm leading-relaxed">{qa.answer}</p>
          {qa.citations.length > 0 && (
            <div className="mt-4 space-y-2">
              <p className="text-xs text-slate-500 uppercase tracking-wide">Sources</p>
              {qa.citations.slice(0, 3).map((c, i) => (
                <button
                  key={`${c.documentId}-${i}`}
                  onClick={() => navigate(`/documents/${c.documentId}`)}
                  className="w-full text-left bg-slate-800/70 hover:bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 transition-colors"
                >
                  <p className="text-xs text-indigo-300 mb-1">{c.title}</p>
                  <p className="text-xs text-slate-400 line-clamp-2">{c.excerpt}</p>
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {loading && (
        <div className="space-y-3 animate-pulse">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="bg-slate-900 border border-slate-800 rounded-xl p-5">
              <div className="flex items-start gap-4">
                <div className="w-9 h-9 bg-slate-800 rounded-lg shrink-0" />
                <div className="flex-1 space-y-2">
                  <div className="flex justify-between gap-4">
                    <div className="h-3.5 bg-slate-800 rounded w-2/5" />
                    <div className="h-3.5 bg-slate-800 rounded w-16" />
                  </div>
                  <div className="h-3 bg-slate-800 rounded w-full" />
                  <div className="h-3 bg-slate-800 rounded w-3/4" />
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {!loading && error && (
        <div className="flex items-center gap-3 bg-red-900/20 border border-red-900/30 rounded-xl p-4 mb-6">
          <AlertCircle className="w-5 h-5 text-red-400 shrink-0" />
          <p className="text-red-400 text-sm">{error}</p>
        </div>
      )}

      {!loading && searched && !error && results.length === 0 && (
        <div className="text-center py-16">
          <p className="text-slate-400">No results for <span className="text-white">"{query}"</span></p>
          <p className="text-slate-600 text-sm mt-1">Try different keywords or check if documents are indexed</p>
        </div>
      )}

      {!loading && results.length > 0 && (
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

      {!loading && !searched && (
        <div className="text-center py-16 text-slate-600">
          <p className="text-sm">Press Enter or click Search to find documents</p>
        </div>
      )}
    </div>
  )
}
