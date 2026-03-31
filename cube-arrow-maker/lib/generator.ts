import { Arrow, GridSize, Difficulty, CubeGeometry } from '@/types'
import { generateCubeGeometry, gridKey } from './cube'
import { v4 as uuid } from 'uuid'

// ─── Constants ─────────────────────────────────────────────────────────────────

const MAX_OVERALL_ATTEMPTS = 5000

// ─── Public API ────────────────────────────────────────────────────────────────

export function autoGenerateLevel(
  gridSize: GridSize,
  maxPathLen: number,
  difficulty: Difficulty,
  straightness: number = 0.5
): Arrow[] {
  const geometry = generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z)
  // Use the user's maxPathLen directly — the UI controls arrow length
  const maxLen = Math.max(2, maxPathLen)

  for (let attempt = 0; attempt < MAX_OVERALL_ATTEMPTS; attempt++) {
    const paths = greedyPartition(geometry, gridSize, maxLen, straightness)
    if (!paths) continue

    const arrows = greedyAssign(paths, geometry, gridSize)
    if (arrows) return shuffled(arrows)
  }

  console.warn('Level generation failed for grid', gridSize)
  return []
}

// ─── Exit-Aware Greedy Partition ───────────────────────────────────────────────
// Key insight: if every path has at least one endpoint that exits IMMEDIATELY
// (trajectory length = 0), then greedy assignment almost always succeeds because
// those arrows can never be blocked by other arrows' bodies.
//
// An "immediate exit" means the arrow head is at a boundary vertex and the
// direction points outward: next step lands outside the grid bounds.

function exitsImmediately(
  lastV: number,
  headV: number,
  geometry: CubeGeometry,
  gridSize: GridSize
): boolean {
  const [lx, ly, lz] = geometry.gridCoords[lastV]
  const [hx, hy, hz] = geometry.gridCoords[headV]
  const ex = hx + (hx - lx)
  const ey = hy + (hy - ly)
  const ez = hz + (hz - lz)
  return ex < 0 || ex >= gridSize.x || ey < 0 || ey >= gridSize.y || ez < 0 || ez >= gridSize.z
}

function greedyPartition(
  geometry: CubeGeometry,
  gridSize: GridSize,
  maxLen: number,
  straightness: number
): number[][] | null {
  const numV = geometry.vertices.length
  const adj: number[][] = Array.from({ length: numV }, () => [])
  for (const [u, v] of geometry.edges) { adj[u].push(v); adj[v].push(u) }

  const visited = new Uint8Array(numV)
  const paths: number[][] = []
  let covered = 0

  while (covered < numV) {
    // Warnsdorff: pick unvisited vertex with fewest free neighbours
    let minDeg = Infinity
    let startV = -1
    for (let i = 0; i < numV; i++) {
      if (visited[i]) continue
      let deg = 0
      for (const nb of adj[i]) if (!visited[nb]) deg++
      if (deg === 0 && covered + 1 < numV) return null
      if (deg < minDeg) { minDeg = deg; startV = i }
    }
    if (startV === -1) break

    const path: number[] = [startV]
    visited[startV] = 1

    let lastDir: [number, number, number] | null = null

    while (path.length < maxLen) {
      const lastIdx = path[path.length - 1]
      const [lx, ly, lz] = geometry.gridCoords[lastIdx]

      // Warnsdorff: sort neighbours by fewest free exits + straightness bias + random jitter
      const candidates: { id: number; deg: number; isStraight: boolean }[] = []
      for (const nb of adj[lastIdx]) {
        if (visited[nb]) continue
        let deg = 0
        for (const nn of adj[nb]) if (!visited[nn]) deg++
        
        const [nx, ny, nz] = geometry.gridCoords[nb]
        const dx = nx - lx, dy = ny - ly, dz = nz - lz
        const isStraight = lastDir !== null && dx === lastDir[0] && dy === lastDir[1] && dz === lastDir[2]
        
        candidates.push({ id: nb, deg, isStraight })
      }

      if (candidates.length === 0) break
 
       // CRITICAL STEP: If any neighbor has only one remaining neighbor (the current one),
       // we MUST visit it now, or it will be isolated (deg=0) and cause failure.
       const critical = candidates.find(c => c.deg === 1)
       const chosen = critical || candidates.sort((a, b) => {
         let scoreA = a.deg
         let scoreB = b.deg
         const biasWeight = (straightness - 0.5) * 3.0
         if (a.isStraight) scoreA -= biasWeight
         if (b.isStraight) scoreB -= biasWeight
         return scoreA - scoreB + (Math.random() - 0.5) * 1.0
       })[0]
 
       const [cx, cy, cz] = geometry.gridCoords[chosen.id]
      lastDir = [cx - lx, cy - ly, cz - lz]

      visited[chosen.id] = 1
      path.push(chosen.id)
    }

    if (path.length < 2) return null
    paths.push(path)
    covered += path.length
  }

  return covered === numV ? paths : null
}

// ─── Exit Info ─────────────────────────────────────────────────────────────────

interface ExitInfo {
  exitsCleanly: boolean
  selfBlocked: boolean
  trajectory: number[]
}

function computeExitInfo(
  path: number[],
  headEnd: 'start' | 'end',
  geometry: CubeGeometry,
  gridSize: GridSize
): ExitInfo {
  if (path.length < 2) return { exitsCleanly: true, selfBlocked: false, trajectory: [] }

  const { gridCoords, posMap } = geometry
  const [tailV, headV] =
    headEnd === 'end'
      ? [path[path.length - 2], path[path.length - 1]]
      : [path[1], path[0]]

  const [tx, ty, tz] = gridCoords[tailV]
  const [hx, hy, hz] = gridCoords[headV]
  const dx = hx - tx, dy = hy - ty, dz = hz - tz

  const ownVerts = new Set(path)
  const trajectory: number[] = []
  let cx = hx, cy = hy, cz = hz

  while (true) {
    cx += dx; cy += dy; cz += dz
    if (cx < 0 || cx >= gridSize.x || cy < 0 || cy >= gridSize.y || cz < 0 || cz >= gridSize.z) {
      return { exitsCleanly: true, selfBlocked: false, trajectory }
    }
    const nextV = posMap.get(gridKey(cx, cy, cz))
    if (nextV === undefined) return { exitsCleanly: false, selfBlocked: false, trajectory }
    if (ownVerts.has(nextV)) return { exitsCleanly: false, selfBlocked: true, trajectory }
    trajectory.push(nextV)
  }
}

// ─── Phase 2: Greedy Assignment ────────────────────────────────────────────────

function edgeKey(a: number, b: number): string {
  return a < b ? `${a},${b}` : `${b},${a}`
}

function isEdgeOrCorner(v: number, geometry: CubeGeometry, gridSize: GridSize): boolean {
  const [x, y, z] = geometry.gridCoords[v]
  const boundaryCount = [
    x === 0 || x === gridSize.x - 1,
    y === 0 || y === gridSize.y - 1,
    z === 0 || z === gridSize.z - 1
  ].filter(Boolean).length
  return boundaryCount >= 2
}

function greedyAssign(
  paths: number[][],
  geometry: CubeGeometry,
  gridSize: GridSize
): Arrow[] | null {

  const exitCache: [ExitInfo, ExitInfo][] = paths.map((path) => [
    computeExitInfo(path, 'start', geometry, gridSize),
    computeExitInfo(path, 'end',   geometry, gridSize),
  ])

  // Early reject: if any path has NO valid exit, this partition is useless
  for (let i = 0; i < paths.length; i++) {
    const s = exitCache[i][0]
    const e = exitCache[i][1]
    const sOk = s.exitsCleanly && !s.selfBlocked
    const eOk = e.exitsCleanly && !e.selfBlocked
    if (!sOk && !eOk) return null
  }

  // Sort: longest trajectory first = last to exit = fewest obstacles
  //       shortest trajectory last = first to exit = needs clear path
  const order = Array.from({ length: paths.length }, (_, i) => i)
  order.sort((a, b) => {
    const aS = exitCache[a][0], aE = exitCache[a][1]
    const bS = exitCache[b][0], bE = exitCache[b][1]
    const aMin = Math.min(
      aS.exitsCleanly && !aS.selfBlocked ? aS.trajectory.length : 999,
      aE.exitsCleanly && !aE.selfBlocked ? aE.trajectory.length : 999,
    )
    const bMin = Math.min(
      bS.exitsCleanly && !bS.selfBlocked ? bS.trajectory.length : 999,
      bE.exitsCleanly && !bE.selfBlocked ? bE.trajectory.length : 999,
    )
    return bMin - aMin + (Math.random() - 0.5) * 1.0
  })

  const occupiedVerts = new Set<number>()
  const occupiedEdges = new Set<string>()
  const placed: Arrow[] = []

  for (const pathIdx of order) {
    const path = paths[pathIdx]
    const sInfo = exitCache[pathIdx][0]
    const eInfo = exitCache[pathIdx][1]
    
    const sOk = sInfo.exitsCleanly && !sInfo.selfBlocked
    const eOk = eInfo.exitsCleanly && !eInfo.selfBlocked
    const sLen = sOk ? sInfo.trajectory.length : 999
    const eLen = eOk ? eInfo.trajectory.length : 999

    // Try shorter trajectory first
    const headEnds: ('start' | 'end')[] = sLen <= eLen ? ['start', 'end'] : ['end', 'start']

    let assigned = false
    for (const headEnd of headEnds) {
      const info = headEnd === 'start' ? sInfo : eInfo
      if (!info.exitsCleanly || info.selfBlocked) continue

      // Check trajectory is clear
      if (info.trajectory.length === 0) {
        // Immediate exit — always valid, never blocked
        for (const v of path) occupiedVerts.add(v)
        for (let j = 0; j < path.length - 1; j++) occupiedEdges.add(edgeKey(path[j], path[j+1]))
        placed.push({ id: uuid(), path, headEnd })
        assigned = true
        break
      }

      // Check trajectory against placed arrows
      const headV = headEnd === 'end' ? path[path.length - 1] : path[0]
      let prevV = headV
      let blocked = false
      for (const v of info.trajectory) {
        if (occupiedVerts.has(v) || occupiedEdges.has(edgeKey(prevV, v))) {
          blocked = true
          break
        }
        prevV = v
      }

      if (!blocked) {
        for (const v of path) occupiedVerts.add(v)
        for (let j = 0; j < path.length - 1; j++) occupiedEdges.add(edgeKey(path[j], path[j+1]))
        placed.push({ id: uuid(), path, headEnd })
        assigned = true
        break
      }
    }

    if (!assigned) return null
  }

  return placed
}

// ─── Util ──────────────────────────────────────────────────────────────────────

function shuffled<T>(arr: T[]): T[] {
  const a = [...arr]
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[a[i], a[j]] = [a[j], a[i]]
  }
  return a
}
