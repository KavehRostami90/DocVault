import { useState } from 'react'
import { ChevronFirst, ChevronLast, ChevronLeft, ChevronRight } from 'lucide-react'

interface PaginationProps {
  page: number
  totalPages: number
  onPageChange: (page: number) => void
  className?: string
}

function buildPageWindows(current: number, total: number): (number | '...')[] {
  if (total <= 7) {
    return Array.from({ length: total }, (_, i) => i + 1)
  }

  const delta = 2
  const items: (number | '...')[] = [1]

  const rangeStart = Math.max(2, current - delta)
  const rangeEnd = Math.min(total - 1, current + delta)

  if (rangeStart > 2) items.push('...')
  for (let i = rangeStart; i <= rangeEnd; i++) items.push(i)
  if (rangeEnd < total - 1) items.push('...')
  items.push(total)

  return items
}

export default function Pagination({ page, totalPages, onPageChange, className = '' }: PaginationProps) {
  const [jumpValue, setJumpValue] = useState('')

  if (totalPages <= 1) return null

  const pages = buildPageWindows(page, totalPages)

  function handleJump(e: React.FormEvent) {
    e.preventDefault()
    const n = parseInt(jumpValue, 10)
    if (!isNaN(n) && n >= 1 && n <= totalPages) {
      onPageChange(n)
    }
    setJumpValue('')
  }

  const btnBase =
    'inline-flex items-center justify-center rounded-lg border text-sm font-medium transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:opacity-40 disabled:cursor-not-allowed'
  const btnIcon = `${btnBase} w-8 h-8 border-slate-700 bg-slate-900 text-slate-400 hover:text-white hover:border-slate-600`
  const btnPage = (active: boolean) =>
    `${btnBase} min-w-[2rem] h-8 px-2 ${
      active
        ? 'border-indigo-500 bg-indigo-600/20 text-indigo-300 cursor-default'
        : 'border-slate-700 bg-slate-900 text-slate-400 hover:text-white hover:border-slate-600'
    }`

  return (
    <div className={`flex flex-wrap items-center justify-between gap-4 ${className}`}>
      {/* Page info */}
      <p className="text-slate-500 text-sm select-none">
        Page <span className="text-slate-300 font-medium">{page}</span> of{' '}
        <span className="text-slate-300 font-medium">{totalPages}</span>
      </p>

      {/* Navigation */}
      <div className="flex items-center gap-1">
        {/* First */}
        <button
          onClick={() => onPageChange(1)}
          disabled={page === 1}
          className={btnIcon}
          title="First page"
          aria-label="First page"
        >
          <ChevronFirst className="w-4 h-4" />
        </button>

        {/* Previous */}
        <button
          onClick={() => onPageChange(page - 1)}
          disabled={page === 1}
          className={btnIcon}
          title="Previous page"
          aria-label="Previous page"
        >
          <ChevronLeft className="w-4 h-4" />
        </button>

        {/* Page numbers */}
        <div className="flex items-center gap-1 mx-1">
          {pages.map((p, i) =>
            p === '...' ? (
              <span key={`ellipsis-${i}`} className="w-8 text-center text-slate-600 text-sm select-none">
                …
              </span>
            ) : (
              <button
                key={p}
                onClick={() => onPageChange(p)}
                disabled={p === page}
                className={btnPage(p === page)}
                aria-label={`Page ${p}`}
                aria-current={p === page ? 'page' : undefined}
              >
                {p}
              </button>
            )
          )}
        </div>

        {/* Next */}
        <button
          onClick={() => onPageChange(page + 1)}
          disabled={page === totalPages}
          className={btnIcon}
          title="Next page"
          aria-label="Next page"
        >
          <ChevronRight className="w-4 h-4" />
        </button>

        {/* Last */}
        <button
          onClick={() => onPageChange(totalPages)}
          disabled={page === totalPages}
          className={btnIcon}
          title="Last page"
          aria-label="Last page"
        >
          <ChevronLast className="w-4 h-4" />
        </button>
      </div>

      {/* Jump to page */}
      <form onSubmit={handleJump} className="flex items-center gap-2">
        <label className="text-slate-500 text-sm select-none whitespace-nowrap">Go to</label>
        <input
          type="number"
          min={1}
          max={totalPages}
          value={jumpValue}
          onChange={e => setJumpValue(e.target.value)}
          placeholder="page"
          className="w-16 bg-slate-900 border border-slate-700 rounded-lg px-2 py-1 text-sm text-white text-center focus:outline-none focus:border-indigo-500 transition-colors [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none"
        />
        <button
          type="submit"
          className="px-3 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 border border-slate-700 text-slate-300 hover:text-white text-sm transition-colors"
        >
          Go
        </button>
      </form>
    </div>
  )
}
