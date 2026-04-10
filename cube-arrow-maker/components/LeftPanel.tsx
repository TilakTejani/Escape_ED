'use client'

import { useState } from 'react'
import { useLevelStore } from '@/store/levelStore'
import { EditorMode } from '@/types'

const MODES: { id: EditorMode; label: string; icon: string; desc: string }[] = [
  { id: 'add', label: 'Add Arrow', icon: '✏', desc: 'Click vertices to build paths' },
  { id: 'select', label: 'Select', icon: '↖', desc: 'Click arrows to edit' },
  { id: 'test', label: 'Test', icon: '▶', desc: 'Simulate gameplay' },
]

export default function LeftPanel() {
  const {
    mode, setMode,
    pendingPath, pendingHeadEnd, setPendingHeadEnd, confirmArrow, cancelPending,
    pendingError, clearPendingError,
    selectedArrowId, selectArrow, arrows, deleteArrow,
    removedInTest, hideBlocked, toggleHideBlocked,
  } = useLevelStore()

  const selectedArrow = arrows.find((a) => a.id === selectedArrowId)
  const solved = mode === 'test' && removedInTest.length === arrows.length && arrows.length > 0

  return (
    <aside className="w-64 h-full bg-white border-r border-slate-200 flex flex-col overflow-y-auto shadow-sm">
      {/* Logo */}
      <div className="px-5 py-4 border-b border-slate-100">
        <h1 className="text-sm font-semibold text-violet-600 tracking-widest uppercase" style={{ fontFamily: 'Outfit, sans-serif' }}>
          Cube Arrow
        </h1>
        <p className="text-xs text-slate-400 mt-0.5">Level Maker</p>
      </div>

      {/* Mode selector */}
      <div className="px-4 py-4 border-b border-slate-100">
        <p className="text-[10px] text-slate-400 uppercase tracking-widest mb-2">Mode</p>
        <div className="flex flex-col gap-1.5">
          {MODES.map((m) => (
            <button
              key={m.id}
              onClick={() => setMode(m.id)}
              className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-left transition-all duration-150 cursor-pointer ${
                mode === m.id
                  ? 'bg-violet-50 border border-violet-200 text-violet-700'
                  : 'border border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-50'
              }`}
            >
              <span className="text-base w-5 text-center">{m.icon}</span>
              <div>
                <p className="text-xs font-medium" style={{ fontFamily: 'Outfit, sans-serif' }}>{m.label}</p>
                <p className="text-[10px] text-slate-400">{m.desc}</p>
              </div>
            </button>
          ))}
        </div>
      </div>



      {/* Add mode */}
      {mode === 'add' && (
        <div className="px-4 py-4 border-b border-slate-100">
          <p className="text-[10px] text-slate-400 uppercase tracking-widest mb-3">Building Arrow</p>

          {pendingPath.length === 0 ? (
            <div className="bg-slate-50 rounded-lg p-3 text-center">
              <p className="text-xs text-slate-500">
                Click a <span className="text-violet-600 font-medium">vertex dot</span> on the cube to start
              </p>
            </div>
          ) : (
            <>
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs text-slate-600">{pendingPath.length} vertices selected</span>
                <span className={`text-[10px] px-2 py-0.5 rounded-full ${
                  pendingPath.length >= 2
                    ? 'text-emerald-600 bg-emerald-50'
                    : 'text-amber-600 bg-amber-50'
                }`}>
                  {pendingPath.length >= 2 ? 'Ready' : 'Need 2+'}
                </span>
              </div>

              <p className="text-[10px] text-slate-400 uppercase tracking-widest mb-2">Arrowhead at</p>
              <div className="flex gap-2 mb-3">
                {(['start', 'end'] as const).map((end) => (
                  <button
                    key={end}
                    onClick={() => setPendingHeadEnd(end)}
                    className={`flex-1 py-1.5 rounded-md text-xs font-medium transition-all cursor-pointer ${
                      pendingHeadEnd === end
                        ? 'bg-violet-600 text-white'
                        : 'bg-slate-100 text-slate-500 hover:bg-slate-200'
                    }`}
                    style={{ fontFamily: 'Outfit, sans-serif' }}
                  >
                    {end === 'start' ? '← Start' : 'End →'}
                  </button>
                ))}
              </div>

              {pendingError && (
                <div className="mb-3 px-3 py-2 rounded-lg bg-red-50 border border-red-200">
                  <p className="text-[11px] text-red-600 leading-snug">{pendingError}</p>
                </div>
              )}

              <div className="flex gap-2">
                <button
                  onClick={() => { clearPendingError(); confirmArrow() }}
                  disabled={pendingPath.length < 2}
                  className="flex-1 py-2 rounded-lg bg-violet-600 hover:bg-violet-700 disabled:opacity-40 disabled:cursor-not-allowed text-xs font-semibold text-white transition-all cursor-pointer"
                  style={{ fontFamily: 'Outfit, sans-serif' }}
                >
                  Add Arrow
                </button>
                <button
                  onClick={cancelPending}
                  className="px-3 py-2 rounded-lg bg-slate-100 hover:bg-slate-200 text-xs text-slate-500 transition-all cursor-pointer"
                >
                  ✕
                </button>
              </div>
            </>
          )}
        </div>
      )}

      {/* Select mode */}
      {mode === 'select' && selectedArrow && (
        <div className="px-4 py-4 border-b border-slate-100">
          <p className="text-[10px] text-slate-400 uppercase tracking-widest mb-3">Selected Arrow</p>

          <div className="bg-slate-50 rounded-lg p-3 mb-3">
            <p className="text-xs text-slate-700">Path: {selectedArrow.path.join(' → ')}</p>
            <p className="text-xs text-slate-400 mt-1">
              Head: {selectedArrow.headEnd} · {selectedArrow.path.length - 1} edge{selectedArrow.path.length !== 2 ? 's' : ''}
            </p>
          </div>

          <button
            onClick={() => deleteArrow(selectedArrow.id)}
            className="w-full py-2 rounded-lg bg-red-50 hover:bg-red-100 border border-red-200 text-xs font-medium text-red-600 transition-all cursor-pointer"
            style={{ fontFamily: 'Outfit, sans-serif' }}
          >
            Delete Arrow
          </button>
        </div>
      )}

      {/* Test mode */}
      {mode === 'test' && (
        <div className="px-4 py-4 border-b border-slate-100">
          <p className="text-[10px] text-slate-400 uppercase tracking-widest mb-3">Test Mode</p>

          {solved ? (
            <div className="bg-emerald-50 border border-emerald-200 rounded-lg p-3 mb-3 text-center">
              <p className="text-emerald-700 text-sm font-semibold" style={{ fontFamily: 'Outfit, sans-serif' }}>Level Solved!</p>
              <p className="text-emerald-500 text-xs mt-1">All arrows cleared</p>
            </div>
          ) : (
            <div className="bg-slate-50 rounded-lg p-3 mb-3">
              <div className="flex justify-between text-xs text-slate-600 mb-2">
                <span>Progress</span>
                <span>{removedInTest.length} / {arrows.length}</span>
              </div>
              <div className="h-1.5 bg-slate-200 rounded-full overflow-hidden">
                <div
                  className="h-full bg-violet-500 rounded-full transition-all"
                  style={{ width: `${arrows.length ? (removedInTest.length / arrows.length) * 100 : 0}%` }}
                />
              </div>
            </div>
          )}

          {!hideBlocked && (
            <p className="text-[10px] text-slate-400 mb-3">
              Red arrows are blocked. Tap in the correct order to clear.
            </p>
          )}

          <button
            onClick={toggleHideBlocked}
            className={`w-full py-2 rounded-lg text-xs font-medium transition-all cursor-pointer mb-2 border ${
              hideBlocked
                ? 'bg-slate-800 text-white border-slate-800'
                : 'bg-slate-50 text-slate-600 border-slate-200 hover:bg-slate-100'
            }`}
            style={{ fontFamily: 'Outfit, sans-serif' }}
          >
            {hideBlocked ? 'Show blocked' : 'Hide blocked'}
          </button>
        </div>
      )}

      {/* Arrow list */}
      <div className="px-4 py-4 flex-1">
        <p className="text-[10px] text-slate-400 uppercase tracking-widest mb-2">
          Arrows ({arrows.length})
        </p>
        {arrows.length === 0 ? (
          <p className="text-xs text-slate-400">No arrows placed yet</p>
        ) : (
          <div className="flex flex-col gap-1.5">
            {arrows.map((a, i) => (
              <div
                key={a.id}
                className={`flex items-center gap-2 px-3 py-2 rounded-lg cursor-pointer transition-all ${
                  selectedArrowId === a.id
                    ? 'bg-violet-50 border border-violet-200 shadow-sm ring-1 ring-violet-200'
                    : 'bg-slate-50 hover:bg-slate-100 border border-transparent'
                }`}
                onClick={() => selectArrow(a.id)}
              >
                <span className="text-xs text-slate-600 flex-1">
                  Arrow {i + 1} · {a.path.length - 1} seg
                </span>
                {mode === 'test' && removedInTest.includes(a.id) && (
                  <span className="text-[10px] text-emerald-500">✓</span>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </aside>
  )
}
