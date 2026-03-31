'use client'

import dynamic from 'next/dynamic'
import LeftPanel from './LeftPanel'
import BottomBar from './BottomBar'
import GridSizePanel from './GridSizePanel'

const Canvas3D = dynamic(() => import('./Canvas3D'), { ssr: false })

export default function LevelMaker() {
  return (
    <div className="flex h-screen w-screen bg-slate-100 overflow-hidden">
      <LeftPanel />
      <div className="flex flex-col flex-1 min-w-0">
        <div className="flex-1 relative">
          <Canvas3D />
          {/* Floating grid size panel — top right of canvas */}
          <div className="absolute top-4 right-4 z-10 pointer-events-auto">
            <GridSizePanel />
          </div>
          <div className="absolute bottom-4 left-1/2 -translate-x-1/2 pointer-events-none">
            <div className="bg-white/80 backdrop-blur-sm border border-slate-200 rounded-full px-4 py-1.5 shadow-sm">
              <p className="text-[11px] text-slate-400">
                Drag to orbit · Scroll to zoom · Click vertices to draw
              </p>
            </div>
          </div>
        </div>
        <BottomBar />
      </div>
    </div>
  )
}
