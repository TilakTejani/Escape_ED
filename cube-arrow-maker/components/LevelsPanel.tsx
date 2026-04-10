'use client'

import { useState, useMemo } from 'react'
import { useLevelStore } from '@/store/levelStore'
import { SavedLevel } from '@/types'

function formatDate(ts: number): string {
  const d = new Date(ts)
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

function LevelRow({
  entry,
  isCurrent,
  onLoad,
  onDelete,
  onRename,
  onDownload,
}: {
  entry: SavedLevel
  isCurrent: boolean
  onLoad: () => void
  onDelete: () => void
  onRename: (name: string) => void
  onDownload: () => void
}) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(entry.name)

  const commitRename = () => {
    const trimmed = draft.trim()
    if (trimmed && trimmed !== entry.name) onRename(trimmed)
    else setDraft(entry.name)
    setEditing(false)
  }

  return (
    <div
      className={`group flex items-center gap-2 px-3 py-2.5 rounded-xl transition-all cursor-pointer ${
        isCurrent
          ? 'bg-violet-50 border border-violet-200'
          : 'hover:bg-slate-50 border border-transparent'
      }`}
      onClick={() => !editing && onLoad()}
    >
      {/* Icon */}
      <div className={`w-7 h-7 rounded-lg flex items-center justify-center flex-shrink-0 text-xs ${
        isCurrent ? 'bg-violet-500 text-white' : 'bg-slate-100 text-slate-400'
      }`}>
        ▦
      </div>

      {/* Name + meta */}
      <div className="flex-1 min-w-0">
        {editing ? (
          <input
            autoFocus
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onBlur={commitRename}
            onKeyDown={(e) => {
              if (e.key === 'Enter') commitRename()
              if (e.key === 'Escape') { setDraft(entry.name); setEditing(false) }
            }}
            onClick={(e) => e.stopPropagation()}
            className="w-full text-xs font-semibold text-slate-700 bg-white border border-violet-300 rounded px-1.5 py-0.5 outline-none focus:ring-1 focus:ring-violet-400"
          />
        ) : (
          <p className="text-xs font-semibold text-slate-700 truncate leading-tight">{entry.name}</p>
        )}
        <p className="text-[9px] text-slate-400 font-medium mt-0.5">
          {entry.level.arrows.length} arrows · {entry.level.gridSize.x}×{entry.level.gridSize.y}×{entry.level.gridSize.z} · {formatDate(entry.savedAt)}
        </p>
      </div>

      {/* Actions — visible on hover */}
      {!editing && (
        <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity" onClick={(e) => e.stopPropagation()}>
          <button
            title="Download"
            onClick={onDownload}
            className="w-6 h-6 rounded-lg bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-[10px] text-slate-500 transition-colors"
          >
            ↓
          </button>
          <button
            title="Rename"
            onClick={() => { setDraft(entry.name); setEditing(true) }}
            className="w-6 h-6 rounded-lg bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-[10px] text-slate-500 transition-colors"
          >
            ✏️
          </button>
          <button
            title="Delete"
            onClick={() => {
              if (confirm(`Delete "${entry.name}"?`)) onDelete()
            }}
            className="w-6 h-6 rounded-lg bg-slate-100 hover:bg-red-100 flex items-center justify-center text-[10px] text-slate-500 hover:text-red-500 transition-colors"
          >
            🗑
          </button>
        </div>
      )}
    </div>
  )
}

export default function LevelsPanel() {
  const {
    savedLevels,
    currentLevelId,
    currentLevelName,
    arrows,
    geometry,
    setCurrentLevelName,
    saveCurrentLevel,
    loadLevel,
    deleteLevel,
    renameLevel,
    newLevel,
    exportLevel,
    importLevel,
  } = useLevelStore()

  const stats = useMemo(() => {
    if (arrows.length === 0) return null
    let turns = 0, segments = 0
    for (const arrow of arrows) {
      if (arrow.path.length <= 2) continue
      segments += arrow.path.length - 2
      let lastDir: [number, number, number] | null = null
      for (let i = 1; i < arrow.path.length; i++) {
        const [ux, uy, uz] = geometry.gridCoords[arrow.path[i - 1]]
        const [vx, vy, vz] = geometry.gridCoords[arrow.path[i]]
        const dir: [number, number, number] = [vx - ux, vy - uy, vz - uz]
        if (lastDir && (dir[0] !== lastDir[0] || dir[1] !== lastDir[1] || dir[2] !== lastDir[2])) turns++
        lastDir = dir
      }
    }
    const turnPct = segments > 0 ? Math.round((turns / segments) * 100) : 0
    const coveredVerts = new Set(arrows.flatMap(a => a.path)).size
    const totalVerts = geometry.vertices.length
    return { turnPct, coveredVerts, totalVerts }
  }, [arrows, geometry])

  const [nameInput, setNameInput] = useState('')
  const [showNameInput, setShowNameInput] = useState(false)
  const [importError, setImportError] = useState<string | null>(null)

  const handleSave = () => {
    saveCurrentLevel()
  }

  const commitSave = () => {
    const name = nameInput.trim() || 'Untitled Level'
    saveCurrentLevel(name)
    setCurrentLevelName(name)
    setShowNameInput(false)
  }

  const handleExportJSON = () => {
    const level = exportLevel()
    const blob = new Blob([JSON.stringify(level, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${currentLevelName.replace(/\s+/g, '_')}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handleImportJSON = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      try {
        const level = JSON.parse(ev.target?.result as string)
        if (!level.gridSize || !Array.isArray(level.arrows)) throw new Error('Invalid level format')
        importLevel(level)
        setImportError(null)
      } catch (err) {
        setImportError('Invalid level file')
      }
    }
    reader.readAsText(file)
    e.target.value = ''
  }

  return (
    <div className="w-72 bg-white/90 backdrop-blur-md border border-slate-200 rounded-2xl p-5 shadow-2xl flex flex-col gap-4 font-outfit select-none pointer-events-auto">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">Levels</h3>
        <button
          onClick={newLevel}
          className="text-[10px] font-bold text-violet-500 hover:text-violet-700 transition-colors px-2 py-1 rounded-lg hover:bg-violet-50"
        >
          + New
        </button>
      </div>

      {/* Current level info */}
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <div className="flex-1 min-w-0">
            <p className="text-[10px] text-slate-400 font-semibold uppercase tracking-wider mb-1">Current Level</p>
            <p className="text-sm font-bold text-slate-700 truncate">{currentLevelName}</p>
          </div>
          <span className="text-[10px] font-bold text-slate-400 bg-slate-50 px-2 py-1 rounded-lg border border-slate-100">
            {arrows.length} arrows
          </span>
        </div>

        {stats && (
          <div className="flex gap-2">
            <div className="flex-1 bg-violet-50 border border-violet-100 rounded-lg px-2.5 py-1.5">
              <p className="text-[9px] text-violet-400 font-bold uppercase tracking-wider">Turn Rate</p>
              <p className="text-xs font-black text-violet-600 tabular-nums">{stats.turnPct}%</p>
            </div>
            <div className="flex-1 bg-slate-50 border border-slate-100 rounded-lg px-2.5 py-1.5">
              <p className="text-[9px] text-slate-400 font-bold uppercase tracking-wider">Coverage</p>
              <p className="text-xs font-black text-slate-600 tabular-nums">{stats.coveredVerts}<span className="text-slate-400 font-medium">/{stats.totalVerts}</span></p>
            </div>
          </div>
        )}

        {/* Save name prompt */}
        {showNameInput && (
          <div className="flex gap-2 items-center">
            <input
              autoFocus
              value={nameInput}
              onChange={(e) => setNameInput(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') commitSave(); if (e.key === 'Escape') setShowNameInput(false) }}
              placeholder="Level name..."
              className="flex-1 text-xs font-semibold text-slate-700 bg-white border border-violet-300 rounded-lg px-3 py-2 outline-none focus:ring-2 focus:ring-violet-400"
            />
            <button onClick={commitSave} className="px-3 py-2 bg-violet-500 text-white text-xs font-bold rounded-lg hover:bg-violet-600">
              Save
            </button>
          </div>
        )}

        {/* Action buttons */}
        <div className="flex gap-2">
          <button
            onClick={handleSave}
            disabled={arrows.length === 0}
            className="flex-1 py-2 rounded-xl bg-violet-500 hover:bg-violet-600 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-bold transition-all shadow-sm shadow-violet-200"
          >
            Save
          </button>
          <button
            onClick={handleExportJSON}
            className="px-3 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-600 text-xs font-bold transition-all"
            title="Export JSON"
          >
            ↑ JSON
          </button>
          <label className="px-3 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-600 text-xs font-bold transition-all cursor-pointer" title="Import JSON">
            ↓ JSON
            <input type="file" accept=".json" className="hidden" onChange={handleImportJSON} />
          </label>
        </div>

        {importError && (
          <p className="text-[10px] text-red-500 font-semibold">{importError}</p>
        )}
      </div>

      {/* Saved levels list */}
      {savedLevels.length > 0 && (
        <>
          <div className="h-px bg-slate-100 w-full" />
          <div className="flex flex-col gap-0.5 max-h-64 overflow-y-auto -mx-1 px-1">
            {[...savedLevels].reverse().map((entry) => (
              <LevelRow
                key={entry.id}
                entry={entry}
                isCurrent={entry.id === currentLevelId}
                onLoad={() => loadLevel(entry.id)}
                onDelete={() => deleteLevel(entry.id)}
                onRename={(name) => renameLevel(entry.id, name)}
                onDownload={() => {
                  const blob = new Blob([JSON.stringify(entry.level, null, 2)], { type: 'application/json' })
                  const url = URL.createObjectURL(blob)
                  const a = document.createElement('a')
                  a.href = url
                  a.download = `${entry.name.replace(/\s+/g, '_')}.json`
                  a.click()
                  URL.revokeObjectURL(url)
                }}
              />
            ))}
          </div>
        </>
      )}

      {savedLevels.length === 0 && (
        <p className="text-[11px] text-slate-400 text-center py-2">No saved levels yet.</p>
      )}
    </div>
  )
}
