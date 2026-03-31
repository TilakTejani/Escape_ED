'use client'

import { useRef } from 'react'
import { useLevelStore } from '@/store/levelStore'
import { Level } from '@/types'

export default function BottomBar() {
  const { arrows, clearAll, exportLevel, importLevel } = useLevelStore()
  const fileRef = useRef<HTMLInputElement>(null)

  const handleExport = () => {
    const level = exportLevel()
    const blob = new Blob([JSON.stringify(level, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `cube-arrow-level-${Date.now()}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handleImport = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      try {
        const level = JSON.parse(ev.target?.result as string) as Level
        importLevel(level)
      } catch {
        alert('Invalid level file')
      }
    }
    reader.readAsText(file)
    e.target.value = ''
  }

  return (
    <footer className="h-12 bg-white border-t border-slate-200 flex items-center px-5 gap-3 shadow-sm">
      <div className="flex-1" />

      <button
        onClick={() => { if (arrows.length === 0 || confirm('Clear all arrows?')) clearAll() }}
        className="px-3 py-1.5 rounded-md bg-slate-100 hover:bg-red-50 hover:text-red-600 text-xs text-slate-500 transition-all cursor-pointer border border-slate-200 hover:border-red-200"
        style={{ fontFamily: 'Outfit, sans-serif' }}
      >
        Clear
      </button>

      <button
        onClick={() => fileRef.current?.click()}
        className="px-3 py-1.5 rounded-md bg-slate-100 hover:bg-slate-200 text-xs text-slate-600 transition-all cursor-pointer border border-slate-200"
        style={{ fontFamily: 'Outfit, sans-serif' }}
      >
        Import
      </button>
      <input ref={fileRef} type="file" accept=".json" className="hidden" onChange={handleImport} />

      <button
        onClick={handleExport}
        disabled={arrows.length === 0}
        className="px-4 py-1.5 rounded-md bg-violet-600 hover:bg-violet-700 disabled:opacity-40 disabled:cursor-not-allowed text-xs font-semibold text-white transition-all cursor-pointer"
        style={{ fontFamily: 'Outfit, sans-serif' }}
      >
        Export JSON
      </button>
    </footer>
  )
}
