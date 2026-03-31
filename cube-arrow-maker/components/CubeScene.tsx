'use client'

import { useMemo, useRef, useState } from 'react'
import { ThreeEvent } from '@react-three/fiber'
import { OrbitControls, Line } from '@react-three/drei'
import * as THREE from 'three'
import { useLevelStore } from '@/store/levelStore'
import { generateCubeGeometry, edgeKey, getOccupiedEdges, getExitDirection, canArrowExit, getNeighbors } from '@/lib/cube'
import { Arrow } from '@/types'

// Track pointer movement to distinguish click from drag
function useDragGuard() {
  const down = useRef<{ x: number; y: number } | null>(null)
  const onDown = (e: ThreeEvent<PointerEvent>) => {
    down.current = { x: e.clientX, y: e.clientY }
  }
  const isClick = (e: ThreeEvent<PointerEvent>) => {
    if (!down.current) return false
    const dx = e.clientX - down.current.x
    const dy = e.clientY - down.current.y
    return Math.sqrt(dx * dx + dy * dy) < 8  // 8px threshold
  }
  return { onDown, isClick }
}

// ─── Vertex dot ────────────────────────────────────────────────────────────────
function VertexDot({
  position,
  index,
  isInPending,
  isLastInPending,
  isOccupied,
  isReachable,
}: {
  position: [number, number, number]
  index: number
  isInPending: boolean
  isLastInPending: boolean
  isOccupied: boolean
  isReachable: boolean
}) {
  const { mode, addVertexToPending, pendingPath } = useLevelStore()
  const [hovered, setHovered] = useState(false)
  const { onDown, isClick } = useDragGuard()

  const hasPath = pendingPath.length > 0
  const canClick =
    mode === 'add' &&
    !isOccupied &&
    !isInPending &&
    (hasPath ? isReachable : true)

  let color = '#94a3b8'
  if (isLastInPending) color = '#0891B2'
  else if (isInPending) color = '#7C3AED'
  else if (isReachable && hasPath) color = hovered ? '#059669' : '#10b981'
  else if (!hasPath && hovered && canClick) color = '#475569'
  else if (!canClick) color = '#cbd5e1'

  const radius = isLastInPending ? 0.14 : isInPending ? 0.12 : isReachable && hasPath ? 0.14 : hovered ? 0.13 : 0.11

  return (
    <group position={position}>
      {/* Visible sphere */}
      <mesh>
        <sphereGeometry args={[radius, 20, 20]} />
        <meshStandardMaterial
          color={color}
          roughness={0.2}
          metalness={0.3}
          emissive={isReachable && hasPath && !isLastInPending ? '#10b981' : 'black'}
          emissiveIntensity={isReachable && hasPath ? 0.2 : 0}
        />
      </mesh>

      {/* Large invisible hit sphere — 0.28 radius for easy clicking */}
      <mesh
        onPointerOver={(e) => {
          e.stopPropagation()
          setHovered(true)
          document.body.style.cursor = canClick ? 'pointer' : 'default'
        }}
        onPointerOut={() => {
          setHovered(false)
          document.body.style.cursor = 'default'
        }}
        onPointerDown={(e) => { e.stopPropagation(); onDown(e) }}
        onPointerUp={(e: ThreeEvent<PointerEvent>) => {
          e.stopPropagation()
          if (isClick(e) && canClick) addVertexToPending(index)
        }}
      >
        <sphereGeometry args={[0.28, 10, 10]} />
        <meshBasicMaterial transparent opacity={0.001} depthWrite={false} />
      </mesh>

      {/* Pulse ring on reachable vertices */}
      {isReachable && hasPath && (
        <mesh rotation={[Math.PI / 2, 0, 0]}>
          <torusGeometry args={[0.2, 0.02, 8, 24]} />
          <meshStandardMaterial color="#10b981" transparent opacity={0.5} />
        </mesh>
      )}

      {/* Selection ring on last pending vertex */}
      {isLastInPending && (
        <mesh rotation={[Math.PI / 2, 0, 0]}>
          <torusGeometry args={[0.2, 0.025, 8, 24]} />
          <meshStandardMaterial color="#0891B2" transparent opacity={0.8} />
        </mesh>
      )}
    </group>
  )
}

// ─── Edge line ─────────────────────────────────────────────────────────────────
function EdgeLine({
  start,
  end,
  v1,
  v2,
  isInPending,
  isOccupied,
}: {
  start: [number, number, number]
  end: [number, number, number]
  v1: number
  v2: number
  isInPending: boolean
  isOccupied: boolean
}) {
  const { mode, pendingPath, addVertexToPending } = useLevelStore()
  const [hovered, setHovered] = useState(false)
  const { onDown, isClick } = useDragGuard()

  const lastV = pendingPath[pendingPath.length - 1]
  const isClickable =
    mode === 'add' &&
    pendingPath.length > 0 &&
    !isOccupied &&
    !isInPending &&
    (lastV === v1 || lastV === v2)

  let lineColor = '#cbd5e1'
  if (isInPending) lineColor = '#7C3AED'
  else if (isOccupied) lineColor = '#e2e8f0'
  else if (hovered && isClickable) lineColor = '#0891B2'

  const mid: [number, number, number] = [
    (start[0] + end[0]) / 2,
    (start[1] + end[1]) / 2,
    (start[2] + end[2]) / 2,
  ]

  // Box dimensions: ensure minimum 0.18 on each axis for a reliable hit area
  const bx = Math.max(Math.abs(end[0] - start[0]), 0.18)
  const by = Math.max(Math.abs(end[1] - start[1]), 0.18)
  const bz = Math.max(Math.abs(end[2] - start[2]), 0.18)

  return (
    <group>
      <Line
        points={[start, end]}
        color={lineColor}
        lineWidth={isInPending ? 3 : hovered && isClickable ? 2 : 1.5}
      />
      <mesh
        position={mid}
        onPointerOver={(e) => {
          e.stopPropagation()
          setHovered(true)
          if (isClickable) document.body.style.cursor = 'pointer'
        }}
        onPointerOut={() => { setHovered(false); document.body.style.cursor = 'default' }}
        onPointerDown={(e) => { e.stopPropagation(); onDown(e) }}
        onPointerUp={(e: ThreeEvent<PointerEvent>) => {
          e.stopPropagation()
          if (!isClickable || !isClick(e)) return
          const nextV = lastV === v1 ? v2 : v1
          addVertexToPending(nextV)
        }}
      >
        <boxGeometry args={[bx, by, bz]} />
        <meshBasicMaterial transparent opacity={0.001} depthWrite={false} />
      </mesh>
    </group>
  )
}

// ─── Arrow mesh ────────────────────────────────────────────────────────────────
function ArrowMesh({ arrow, isRemoved }: { arrow: Arrow; isRemoved: boolean }) {
  const { mode, selectedArrowId, selectArrow, tapArrow, arrows, removedInTest, gridSize, hideBlocked } = useLevelStore()
  const [hovered, setHovered] = useState(false)
  const { onDown, isClick } = useDragGuard()

  const geometry = useMemo(() => generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z), [gridSize])
  const { vertices } = geometry

  if (isRemoved) return null

  const isSelected = selectedArrowId === arrow.id
  const isTestMode = mode === 'test'

  const remaining = arrows.filter((a) => !removedInTest.includes(a.id))
  const canExit = isTestMode ? canArrowExit(arrow.id, remaining, geometry, gridSize) : true

  const points = arrow.path.map((vi) => new THREE.Vector3(...vertices[vi]))

  const headVertex = arrow.headEnd === 'end'
    ? arrow.path[arrow.path.length - 1]
    : arrow.path[0]
  const headPos = new THREE.Vector3(...vertices[headVertex])
  const dir = getExitDirection(vertices, arrow.path, arrow.headEnd)

  const coneH = 0.12
  const coneR = 0.05

  const lineColor = isSelected ? '#7C3AED' : hovered ? '#475569' : '#1e293b'
  // When hideBlocked is on all visible arrows look the same — no red
  const coneColor = isSelected ? '#7C3AED' : (!hideBlocked && !canExit && isTestMode) ? '#ef4444' : '#1e293b'

  const quaternion = new THREE.Quaternion()
  quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), new THREE.Vector3(...dir))
  const euler = new THREE.Euler().setFromQuaternion(quaternion)

  const handlePointerOver = (e: ThreeEvent<PointerEvent>) => {
    e.stopPropagation()
    setHovered(true)
    if (mode === 'select' || mode === 'test') document.body.style.cursor = 'pointer'
  }
  const handlePointerOut = () => { setHovered(false); document.body.style.cursor = 'default' }
  const handlePointerDown = (e: ThreeEvent<PointerEvent>) => { e.stopPropagation(); onDown(e) }
  const handlePointerUp = (e: ThreeEvent<PointerEvent>) => {
    e.stopPropagation()
    if (!isClick(e)) return
    if (mode === 'select') selectArrow(arrow.id)
    else if (mode === 'test') tapArrow(arrow.id)
  }

  return (
    <group>
      {/* Visible path line */}
      <Line
        points={points}
        color={lineColor}
        lineWidth={isSelected ? 5 : hovered ? 4 : 3.5}
      />

      {/* Invisible hit box per segment — fat clickable area along each edge */}
      {arrow.path.slice(0, -1).map((vi, i) => {
        const a = vertices[vi]
        const b = vertices[arrow.path[i + 1]]
        const mid: [number, number, number] = [
          (a[0] + b[0]) / 2,
          (a[1] + b[1]) / 2,
          (a[2] + b[2]) / 2,
        ]
        const bx = Math.max(Math.abs(b[0] - a[0]), 0.2)
        const by = Math.max(Math.abs(b[1] - a[1]), 0.2)
        const bz = Math.max(Math.abs(b[2] - a[2]), 0.2)
        return (
          <mesh
            key={i}
            position={mid}
            onPointerOver={handlePointerOver}
            onPointerOut={handlePointerOut}
            onPointerDown={handlePointerDown}
            onPointerUp={handlePointerUp}
          >
            <boxGeometry args={[bx, by, bz]} />
            <meshBasicMaterial transparent opacity={0.001} depthWrite={false} />
          </mesh>
        )
      })}

      {/* Arrowhead cone — also clickable */}
      <mesh
        position={[
          headPos.x + dir[0] * 0.06,
          headPos.y + dir[1] * 0.06,
          headPos.z + dir[2] * 0.06,
        ]}
        rotation={euler}
        onPointerOver={handlePointerOver}
        onPointerOut={handlePointerOut}
        onPointerDown={handlePointerDown}
        onPointerUp={handlePointerUp}
      >
        <coneGeometry args={[coneR, coneH, 10]} />
        <meshStandardMaterial
          color={coneColor}
          roughness={0.2}
          metalness={0.3}
          polygonOffset
          polygonOffsetFactor={-1}
          polygonOffsetUnits={-1}
        />
      </mesh>

      {/* Selected ring */}
      {isSelected && (
        <mesh position={headPos}>
          <torusGeometry args={[0.14, 0.02, 8, 24]} />
          <meshStandardMaterial color="#7C3AED" />
        </mesh>
      )}
    </group>
  )
}

// ─── Cube faces ────────────────────────────────────────────────────────────────
function CubeFaces({ gridSize }: { gridSize: { x: number, y: number, z: number } }) {
  const dx = gridSize.x - 1
  const dy = gridSize.y - 1
  const dz = gridSize.z - 1

  const faces: { pos: [number, number, number]; rot: [number, number, number]; size: [number, number] }[] = [
    { pos: [0, 0, dz / 2], rot: [0, 0, 0], size: [dx, dy] },               // front  +Z
    { pos: [0, 0, -dz / 2], rot: [0, Math.PI, 0], size: [dx, dy] },          // back   -Z
    { pos: [dx / 2, 0, 0], rot: [0, Math.PI / 2, 0], size: [dz, dy] },     // right  +X
    { pos: [-dx / 2, 0, 0], rot: [0, -Math.PI / 2, 0], size: [dz, dy] },     // left   -X
    { pos: [0, dy / 2, 0], rot: [-Math.PI / 2, 0, 0], size: [dx, dz] },     // top    +Y
    { pos: [0, -dy / 2, 0], rot: [Math.PI / 2, 0, 0], size: [dx, dz] },     // bottom -Y
  ]

  return (
    <>
      {faces.map(({ pos, rot, size }, i) => (
        <mesh key={i} position={pos} rotation={rot}>
          <planeGeometry args={size} />
          <meshStandardMaterial
            color="#dbeafe"
            side={THREE.DoubleSide}
            polygonOffset
            polygonOffsetFactor={1}
            polygonOffsetUnits={1}
          />
        </mesh>
      ))}
    </>
  )
}

// ─── Main scene ────────────────────────────────────────────────────────────────
export default function CubeScene() {
  const { gridSize, arrows, pendingPath, mode, removedInTest } = useLevelStore()
  const geometry = useMemo(() => generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z), [gridSize])
  const { vertices, edges } = geometry
  const occupied = getOccupiedEdges(arrows)

  const pendingEdges = new Set<string>()
  for (let i = 0; i < pendingPath.length - 1; i++) {
    pendingEdges.add(edgeKey(pendingPath[i], pendingPath[i + 1]))
  }

  // Compute which vertices are reachable from the last pending vertex
  const reachableVertices = new Set<number>()
  if (pendingPath.length > 0) {
    const lastV = pendingPath[pendingPath.length - 1]
    const neighbors = getNeighbors(edges, lastV)
    for (const n of neighbors) {
      if (pendingPath.includes(n)) continue
      const key = edgeKey(lastV, n)
      if (occupied.has(key)) continue
      reachableVertices.add(n)
    }
  }

  // Dynamic values for scaling controls
  const maxDim = Math.max(gridSize.x, gridSize.y, gridSize.z)
  const minCam = maxDim * 0.8
  const maxCam = maxDim * 4

  return (
    <>
      <ambientLight intensity={1.5} />
      <directionalLight position={[maxDim, maxDim * 1.5, maxDim]} intensity={1.2} />
      <directionalLight position={[-maxDim, -maxDim / 2, -maxDim]} intensity={0.5} />

      <OrbitControls
        makeDefault
        enablePan={false}
        enableZoom
        minDistance={minCam}
        maxDistance={maxCam}
        dampingFactor={0.1}
        enableDamping
      />

      {/* Cube face panels */}
      <CubeFaces gridSize={gridSize} />

      {/* Grid edges on cube surface */}
      {edges.map(([a, b], i) => {
        const key = edgeKey(a, b)
        const isInPending = pendingEdges.has(key)
        const isOccupied = occupied.has(key)
        return (
          <EdgeLine
            key={i}
            start={vertices[a]}
            end={vertices[b]}
            v1={a}
            v2={b}
            isInPending={isInPending}
            isOccupied={isOccupied}
          />
        )
      })}

      {/* Vertices */}
      {vertices.map((pos, i) => {
        const isInPending = pendingPath.includes(i)
        const isLastInPending = pendingPath[pendingPath.length - 1] === i
        const isOccupied = arrows.some((a) => a.path.includes(i)) && !isInPending
        const isReachable = reachableVertices.has(i)

        // In test mode: show dots for vertices not covered by any remaining arrow
        if (mode === 'test') {
          const isPartOfArrow = arrows.some((a) => !removedInTest.includes(a.id) && a.path.includes(i))
          if (isPartOfArrow) return null
          return (
            <mesh key={i} position={pos}>
              <sphereGeometry args={[0.03, 8, 8]} />
              <meshStandardMaterial color="#cbd5e1" roughness={0.4} />
            </mesh>
          )
        }

        return (
          <VertexDot
            key={i}
            position={pos}
            index={i}
            isInPending={isInPending}
            isLastInPending={isLastInPending}
            isOccupied={isOccupied}
            isReachable={isReachable}
          />
        )
      })}

      {/* Placed arrows */}
      {arrows.map((arrow) => (
        <ArrowMesh
          key={arrow.id}
          arrow={arrow}
          isRemoved={removedInTest.includes(arrow.id)}
        />
      ))}
    </>
  )
}
