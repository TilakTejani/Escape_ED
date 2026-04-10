'use client'

import { useState, useMemo, useEffect } from 'react'
import { useLevelStore } from '@/store/levelStore'
import { GridSize, Difficulty } from '@/types'

const MIN = 2
const MAX = 10

export default function GridSizePanel() {
  const { arrows, gridSize, setGridSize, generateArrows, straightness, setStraightness, geometry } = useLevelStore()

  const maxDim = Math.max(gridSize.x, gridSize.y, gridSize.z)
  const [genMaxLen, setGenMaxLen] = useState(maxDim)

  // Keep genMaxLen in sync when grid size changes
  useEffect(() => {
    setGenMaxLen(Math.max(gridSize.x, gridSize.y, gridSize.z))
  }, [gridSize])
  const [difficulty, setDifficulty] = useState<Difficulty>('medium')
  const [generating, setGenerating] = useState(false)

  const changeAxis = (axis: keyof GridSize, value: number) => {
    const clamped = Math.min(MAX, Math.max(MIN, value))
    const next = { ...gridSize, [axis]: clamped }
    if (arrows.length > 0 && !confirm('Changing grid size will clear all arrows. Continue?')) return
    setGridSize(next)
  }

  const handleGenerate = () => {
    if (arrows.length > 0 && !confirm('This will replace all current arrows. Continue?')) return
    setGenerating(true)
    setTimeout(() => {
      generateArrows(genMaxLen, difficulty)
      setGenerating(false)
    }, 16)
  }

  const avgTurnRate = useMemo(() => {
    if (arrows.length === 0) return 0
    let totalSegments = 0
    let totalTurns = 0
    
    for (const arrow of arrows) {
      if (arrow.path.length <= 2) continue
      totalSegments += (arrow.path.length - 2) // Segments after the first can be turns
      
      let lastDir: [number, number, number] | null = null
      for (let i = 1; i < arrow.path.length; i++) {
        const [u, v] = [arrow.path[i-1], arrow.path[i]]
        const [ux, uy, uz] = geometry.gridCoords[u]
        const [vx, vy, vz] = geometry.gridCoords[v]
        const dir: [number, number, number] = [vx - ux, vy - uy, vz - uz]
        if (lastDir && (dir[0] !== lastDir[0] || dir[1] !== lastDir[1] || dir[2] !== lastDir[2])) {
          totalTurns++
        }
        lastDir = dir
      }
    }
    return totalSegments > 0 ? (totalTurns / totalSegments) : 0
  }, [arrows, geometry])

  return (
    <div className="w-72 bg-white/90 backdrop-blur-md border border-slate-200 rounded-2xl p-5 shadow-2xl flex flex-col gap-4 font-outfit select-none pointer-events-auto">
      {/* Grid Size Section */}
      <div className="flex flex-col gap-2.5">
        <div className="flex items-center justify-between">
          <h3 className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">Grid Geometry</h3>
          <span className="text-[10px] text-slate-400 font-medium tabular-nums">
            {geometry.vertices.length}V · {geometry.edges.length}E
          </span>
        </div>
        
        <div className="flex flex-col gap-2">
          {(['x', 'y', 'z'] as const).map((axis) => (
            <div key={axis} className="flex items-center gap-3">
              <span className={`w-4 text-center text-[11px] font-black ${
                axis === 'x' ? 'text-rose-500' : axis === 'y' ? 'text-emerald-500' : 'text-violet-500'
              }`}>{axis.toUpperCase()}</span>
              <input
                type="range"
                min={MIN}
                max={MAX}
                value={gridSize[axis]}
                onChange={(e) => changeAxis(axis, parseInt(e.target.value))}
                className={`flex-1 h-1.5 rounded-lg appearance-none bg-slate-100 cursor-pointer accent-${
                  axis === 'x' ? 'rose' : axis === 'y' ? 'emerald' : 'violet'
                }-500`}
              />
              <span className="w-5 text-right text-xs font-bold text-slate-700 tabular-nums">{gridSize[axis]}</span>
            </div>
          ))}
        </div>
      </div>

      <div className="h-px bg-slate-100 w-full" />

      {/* Generation Config Section */}
      <div className="flex flex-col gap-3.5">
        <h3 className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">Generator Settings</h3>
        
        {/* Difficulty */}
        <div className="flex flex-col gap-1.5">
          <span className="text-[10px] text-slate-400 font-semibold px-0.5">Difficulty</span>
          <div className="flex gap-1.5">
            {(['easy', 'medium', 'hard'] as Difficulty[]).map((d) => (
              <button
                key={d}
                onClick={() => setDifficulty(d)}
                className={`flex-1 py-1.5 rounded-lg text-[10px] font-bold capitalize transition-all border ${
                  difficulty === d 
                    ? d === 'easy' ? 'bg-emerald-500 border-emerald-500 text-white shadow-lg shadow-emerald-200'
                    : d === 'medium' ? 'bg-amber-500 border-amber-500 text-white shadow-lg shadow-amber-200'
                    : 'bg-rose-500 border-rose-500 text-white shadow-lg shadow-rose-200'
                    : 'bg-slate-50 border-slate-100 text-slate-400 hover:bg-slate-100'
                }`}
              >
                {d}
              </button>
            ))}
          </div>
        </div>

        {/* Max Length */}
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center justify-between px-0.5">
            <span className="text-[10px] text-slate-400 font-semibold">Max Path Length</span>
            <span className="text-xs font-bold text-slate-700">{genMaxLen}</span>
          </div>
          <input
            type="range"
            min={2}
            max={maxDim}
            value={genMaxLen}
            onChange={(e) => setGenMaxLen(parseInt(e.target.value))}
            className="w-full h-1.5 rounded-lg appearance-none bg-slate-100 cursor-pointer accent-amber-500"
          />
        </div>

        {/* Path Style Bias */}
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center justify-between px-0.5">
             <div className="flex flex-col">
               <span className="text-[10px] text-slate-400 font-semibold tracking-tight">Path Style</span>
               <span className="text-[8px] text-slate-300 font-medium uppercase tracking-tighter">
                 {straightness < 0.3 ? 'Turned' : straightness > 0.7 ? 'Straight' : 'Balanced'}
               </span>
             </div>
            <span className="text-xs font-black text-violet-600 tabular-nums">{(straightness * 100).toFixed(0)}%</span>
          </div>
          <input
            type="range"
            min={0}
            max={100}
            step={5}
            value={straightness * 100}
            onChange={(e) => setStraightness(parseInt(e.target.value) / 100)}
            className="w-full h-1.5 rounded-lg appearance-none bg-slate-100 cursor-pointer accent-violet-500"
          />
        </div>

        {/* Generate Button */}
        <button
          onClick={handleGenerate}
          disabled={generating}
          className="w-full mt-1 px-4 py-3 rounded-xl bg-gradient-to-br from-violet-600 to-indigo-700 hover:from-violet-500 hover:to-indigo-600 active:scale-[0.98] disabled:opacity-50 disabled:active:scale-100 text-white shadow-xl shadow-violet-200 transition-all flex items-center justify-center gap-2.5"
        >
          {generating ? (
            <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
          ) : (
            <span className="text-sm">⚡</span>
          )}
          <span className="text-xs font-bold tracking-wide">
            {generating ? 'Engine Working...' : 'Generate Puzzle'}
          </span>
        </button>
      </div>

      {/* Stats Section */}
      {arrows.length > 0 && (
        <>
          <div className="h-px bg-slate-100 w-full" />
          <div className="flex items-center justify-between">
             <div className="flex flex-col">
               <span className="text-[13px] font-black text-slate-700">{arrows.length} Arrows</span>
               <span className="text-[9px] text-slate-400 font-bold uppercase tracking-tight">Active Level</span>
             </div>
             <div className="flex flex-col items-end">
               <div className="px-2 py-0.5 bg-violet-50 rounded-full border border-violet-100">
                 <span className="text-[10px] font-bold text-violet-600 tabular-nums">
                   {(avgTurnRate * 100).toFixed(0)}% Turn Rate
                 </span>
               </div>
               <span className="text-[9px] text-slate-300 font-medium mt-0.5 uppercase">Avg direction changes</span>
             </div>
          </div>
        </>
      )}
    </div>
  )
}
