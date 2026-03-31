'use client'

import { Canvas } from '@react-three/fiber'
import { Suspense } from 'react'
import CubeScene from './CubeScene'
import { useLevelStore } from '@/store/levelStore'

export default function Canvas3D() {
  const { gridSize } = useLevelStore()
  const maxDim = Math.max(gridSize.x, gridSize.y, gridSize.z)
  // Initial camera position scales with grid size
  const camPos: [number, number, number] = [maxDim * 1.2, maxDim * 1.0, maxDim * 1.2]

  return (
    <div className="w-full h-full">
      <Canvas
        camera={{ position: camPos, fov: 50 }}
        shadows
        gl={{ antialias: true }}
        key={`${gridSize.x}-${gridSize.y}-${gridSize.z}`} // Re-mount on size change to reset camera
      >
        <Suspense fallback={null}>
          <CubeScene />
        </Suspense>
      </Canvas>
    </div>
  )
}
