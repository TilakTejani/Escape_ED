import { Arrow, GridSize, Difficulty, CubeGeometry } from '@/types'
import { generateCubeGeometry, gridKey } from './cube'
import { v4 as uuid } from 'uuid'

function computeHeadDir(
  path: number[],
  headEnd: 'start' | 'end',
  gridCoords: [number, number, number][]
): [number, number, number] {
  const [tailV, headV] = headEnd === 'end'
    ? [path[path.length - 2], path[path.length - 1]]
    : [path[1], path[0]]
  const [tx, ty, tz] = gridCoords[tailV]
  const [hx, hy, hz] = gridCoords[headV]
  return [hx - tx, hy - ty, hz - tz]
}

// ─── Difficulty Configuration ──────────────────────────────────────────────────

interface DifficultyConfig {
  facePenalty: number
  preferredMaxTraj: number
  preferLong: boolean
}

const DIFFICULTY_CONFIG: Record<Difficulty, DifficultyConfig> = {
  easy:   { facePenalty: 8, preferredMaxTraj: 1,   preferLong: false },
  medium: { facePenalty: 2, preferredMaxTraj: 3,   preferLong: false },
  hard:   { facePenalty: 0, preferredMaxTraj: 999, preferLong: true  },
}

// ─── Public API ────────────────────────────────────────────────────────────────

export function autoGenerateLevel(
  gridSize: GridSize,
  maxPathLen: number,
  difficulty: Difficulty,
  straightness: number = 0.5
): Arrow[] {
  const geometry = generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z)
  const config   = DIFFICULTY_CONFIG[difficulty]
  const maxLen   = Math.max(2, maxPathLen)

  // Try several times — each run has different random choices
  for (let attempt = 0; attempt < 20; attempt++) {
    const paths  = buildPaths(geometry, gridSize, maxLen, straightness, config)
    const arrows = assignExits(paths, geometry, gridSize, config)
    if (arrows) return shuffled(arrows)
  }

  // Fallback: any geometric exit, ignoring cross-arrow blocking
  const paths = buildPaths(geometry, gridSize, maxLen, straightness, config)
  return shuffled(fallbackAssign(paths, geometry, gridSize))
}

// ─── Face Helpers ──────────────────────────────────────────────────────────────

function buildFaceMasks(geometry: CubeGeometry, gridSize: GridSize): Uint8Array {
  const { gridCoords } = geometry
  const masks = new Uint8Array(gridCoords.length)
  const { x: nx, y: ny, z: nz } = gridSize
  for (let i = 0; i < gridCoords.length; i++) {
    const [x, y, z] = gridCoords[i]
    let m = 0
    if (x === 0)      m |= 1
    if (x === nx - 1) m |= 2
    if (y === 0)      m |= 4
    if (y === ny - 1) m |= 8
    if (z === 0)      m |= 16
    if (z === nz - 1) m |= 32
    masks[i] = m
  }
  return masks
}

function popcount(n: number): number {
  let c = 0; while (n) { c += n & 1; n >>>= 1 }
  return c
}

// ─── Core: Build Paths ────────────────────────────────────────────────────────
//
// Greedy head-first body-building with isolated-vertex rescue.
//
// Algorithm:
//   1. Pick best unvisited vertex (Warnsdorff) → arrowhead
//   2. Extend body step by step (Warnsdorff + face penalty + straightness + noise)
//   3. After each visit, check if any neighbour became isolated (freeDeg → 0)
//      → rescue it immediately by splitting/extending an adjacent committed path
//   4. When current path can't extend, commit it and start a new arrow
//
// No backtracking. No exponential blowup. Always terminates.

function buildPaths(
  geometry: CubeGeometry,
  gridSize: GridSize,
  maxLen: number,
  straightness: number,
  config: DifficultyConfig
): number[][] {
  const numV = geometry.vertices.length
  const adj: number[][] = Array.from({ length: numV }, () => [])
  for (const [u, v] of geometry.edges) { adj[u].push(v); adj[v].push(u) }

  const { gridCoords } = geometry
  const faceMasks = buildFaceMasks(geometry, gridSize)

  const visited   = new Uint8Array(numV)
  const freeDeg   = new Int32Array(numV)
  for (let v = 0; v < numV; v++) freeDeg[v] = adj[v].length

  // Track which committed path each vertex belongs to and its position within it
  const vertexPath = new Int32Array(numV).fill(-1)
  const vertexPos  = new Int32Array(numV).fill(-1)

  const paths: number[][] = []

  // ── Helpers ────────────────────────────────────────────────────────────────

  const visit = (v: number) => {
    visited[v] = 1
    for (const nb of adj[v]) if (!visited[nb]) freeDeg[nb]--
  }

  const commitPath = (path: number[]) => {
    const pi = paths.length
    paths.push(path)
    for (let k = 0; k < path.length; k++) {
      vertexPath[path[k]] = pi
      vertexPos[path[k]] = k
    }
  }

  // Reindex all vertices in a path after mutation
  const reindex = (pi: number) => {
    const p = paths[pi]
    for (let k = 0; k < p.length; k++) {
      vertexPath[p[k]] = pi
      vertexPos[p[k]] = k
    }
  }

  // Score a candidate next step
  const scoreStep = (
    nb: number, last: number, prev: number | null, pathFaceMask: number
  ): number => {
    const newFaces = popcount(faceMasks[nb] & ~pathFaceMask)
    let score = freeDeg[nb] + newFaces * config.facePenalty * 0.5 + Math.random() * 3.0
    if (prev !== null) {
      const [lx, ly, lz] = gridCoords[last]
      const [px, py, pz] = gridCoords[prev]
      const [nx, ny, nz] = gridCoords[nb]
      const straight =
        (nx - lx) === (lx - px) &&
        (ny - ly) === (ly - py) &&
        (nz - lz) === (lz - pz)
      if (straight) score -= (straightness - 0.5) * 6
    }
    return score
  }

  // Pick an unvisited start vertex: strongly prefer near-isolated vertices,
  // otherwise add heavy noise so arrows don't always start at the same corner.
  const chooseStart = (): number => {
    let best = -1, bestScore = Infinity
    for (let v = 0; v < numV; v++) {
      if (!visited[v]) {
        // Keep Warnsdorff benefit for truly constrained vertices (deg 0-1),
        // but randomise freely among the rest.
        const score = freeDeg[v] <= 1 ? freeDeg[v] : freeDeg[v] + Math.random() * 8
        if (score < bestScore) { bestScore = score; best = v }
      }
    }
    return best
  }

  // ── Rescue: attach isolated vertex v to an adjacent committed path ──────────
  //
  // Tries in order:
  //   1. u is a path tail   → extend that path with v
  //   2. u is a path head   → reverse that path, then extend with v
  //   3. u is in the middle → split at u: left half [..., u, v], right half [...]
  //      if right half becomes a singleton, try to merge it too
  //
  // v must be unvisited when this is called.

  const rescueVertex = (v: number): boolean => {
    // Pass 1: prefer tail/head extension (no split needed)
    for (const u of adj[v]) {
      if (!visited[u]) continue
      const pi = vertexPath[u]
      if (pi < 0) continue
      const p = paths[pi]
      const pos = vertexPos[u]

      if (pos === p.length - 1 && p.length < maxLen) {
        // u is the tail — extend
        p.push(v); visit(v)
        vertexPath[v] = pi; vertexPos[v] = p.length - 1
        return true
      }
      if (pos === 0 && p.length < maxLen) {
        // u is the head — reverse then extend
        p.reverse(); reindex(pi)
        p.push(v); visit(v)
        vertexPath[v] = pi; vertexPos[v] = p.length - 1
        return true
      }
    }

    // Pass 2: split a middle vertex
    for (const u of adj[v]) {
      if (!visited[u]) continue
      const pi = vertexPath[u]
      if (pi < 0) continue
      const p = paths[pi]
      const pos = vertexPos[u]

      // left = p[0..pos] + v,  right = p[pos+1..]
      const left  = [...p.slice(0, pos + 1), v]
      const right = p.slice(pos + 1)
      if (left.length > maxLen) continue

      paths[pi] = left; visit(v)
      reindex(pi)

      if (right.length >= 2) {
        const newPi = paths.length
        paths.push(right)
        reindex(newPi)
      } else if (right.length === 1) {
        // right is a singleton — try to attach it to a neighbouring path tail
        const sv = right[0]
        vertexPath[sv] = -1; vertexPos[sv] = -1
        for (const snb of adj[sv]) {
          const spi = vertexPath[snb]
          if (spi < 0) continue
          const sp = paths[spi]
          const spos = vertexPos[snb]
          if (spos === sp.length - 1 && sp.length < maxLen) {
            sp.push(sv); vertexPath[sv] = spi; vertexPos[sv] = sp.length - 1
            break
          }
          if (spos === 0 && sp.length < maxLen) {
            sp.reverse(); reindex(spi)
            sp.push(sv); vertexPath[sv] = spi; vertexPos[sv] = sp.length - 1
            break
          }
        }
        // If still unattached, it stays as a visited vertex with no path — acceptable edge case
      }
      return true
    }

    return false  // shouldn't happen on a well-connected surface
  }

  // ── Main loop ───────────────────────────────────────────────────────────────

  while (true) {
    const start = chooseStart()
    if (start === -1) break

    const path: number[] = [start]
    let pathFaceMask = faceMasks[start]
    visit(start)

    // Rescue any vertices isolated by visiting start
    for (const nb of adj[start]) {
      if (!visited[nb] && freeDeg[nb] === 0) rescueVertex(nb)
    }

    // Extend the arrow body
    while (path.length < maxLen) {
      const last = path[path.length - 1]
      const prev = path.length >= 2 ? path[path.length - 2] : null

      let bestNb = -1, bestScore = Infinity
      for (const nb of adj[last]) {
        if (!visited[nb]) {
          const s = scoreStep(nb, last, prev, pathFaceMask)
          if (s < bestScore) { bestScore = s; bestNb = nb }
        }
      }
      if (bestNb === -1) break  // no free neighbours — done with this arrow

      path.push(bestNb)
      pathFaceMask |= faceMasks[bestNb]
      visit(bestNb)

      // Rescue any newly isolated vertices
      for (const nb of adj[bestNb]) {
        if (!visited[nb] && freeDeg[nb] === 0) rescueVertex(nb)
      }
    }

    // Commit this arrow (length ≥ 2) or try to merge singleton into adjacent path
    if (path.length >= 2) {
      commitPath(path)
    } else {
      // Singleton: find an adjacent committed path and attach
      const sv = path[0]
      for (const nb of adj[sv]) {
        const pi = vertexPath[nb]
        if (pi < 0) continue
        const p = paths[pi]
        const pos = vertexPos[nb]
        if (pos === p.length - 1 && p.length < maxLen) {
          p.push(sv); vertexPath[sv] = pi; vertexPos[sv] = p.length - 1
          break
        }
        if (pos === 0 && p.length < maxLen) {
          p.reverse(); reindex(pi)
          p.push(sv); vertexPath[sv] = pi; vertexPos[sv] = p.length - 1
          break
        }
      }
    }
  }

  return paths.filter(p => p.length >= 2)
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
  const [tailV, headV] = headEnd === 'end'
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
    if (cx < 0 || cx >= gridSize.x || cy < 0 || cy >= gridSize.y || cz < 0 || cz >= gridSize.z)
      return { exitsCleanly: true, selfBlocked: false, trajectory }
    const nextV = posMap.get(gridKey(cx, cy, cz))
    if (nextV === undefined) return { exitsCleanly: false, selfBlocked: false, trajectory }
    if (ownVerts.has(nextV))  return { exitsCleanly: false, selfBlocked: true,  trajectory }
    trajectory.push(nextV)
  }
}

// ─── Phase 2: Exit Assignment ──────────────────────────────────────────────────

function edgeKey(a: number, b: number): string {
  return a < b ? `${a},${b}` : `${b},${a}`
}

function assignExits(
  paths: number[][],
  geometry: CubeGeometry,
  gridSize: GridSize,
  config: DifficultyConfig
): Arrow[] | null {
  const exitCache: [ExitInfo, ExitInfo][] = paths.map(path => [
    computeExitInfo(path, 'start', geometry, gridSize),
    computeExitInfo(path, 'end',   geometry, gridSize),
  ])

  for (let i = 0; i < paths.length; i++) {
    const [s, e] = exitCache[i]
    if ((!s.exitsCleanly || s.selfBlocked) && (!e.exitsCleanly || e.selfBlocked)) return null
  }

  const validLen = (info: ExitInfo) =>
    info.exitsCleanly && !info.selfBlocked ? info.trajectory.length : 999

  const noise = Array.from({ length: paths.length }, () => Math.random())
  const order = Array.from({ length: paths.length }, (_, i) => i)
  order.sort((a, b) => {
    const aMin = Math.min(validLen(exitCache[a][0]), validLen(exitCache[a][1]))
    const bMin = Math.min(validLen(exitCache[b][0]), validLen(exitCache[b][1]))
    return (config.preferLong ? -1 : 1) * (bMin - aMin) + (noise[a] - noise[b]) * 1.8
  })

  const occupiedVerts = new Set<number>()
  const occupiedEdges = new Set<string>()
  const placed: Arrow[] = []

  for (const pathIdx of order) {
    const path = paths[pathIdx]
    const [sInfo, eInfo] = exitCache[pathIdx]

    const isValid     = (info: ExitInfo) => info.exitsCleanly && !info.selfBlocked
    const isPreferred = (info: ExitInfo) => isValid(info) && info.trajectory.length <= config.preferredMaxTraj

    const sLen = isValid(sInfo) ? sInfo.trajectory.length : 999
    const eLen = isValid(eInfo) ? eInfo.trajectory.length : 999

    const preferred = (['start', 'end'] as const).filter(e => isPreferred(e === 'start' ? sInfo : eInfo))
    const fallback  = (['start', 'end'] as const).filter(e => !isPreferred(e === 'start' ? sInfo : eInfo) && isValid(e === 'start' ? sInfo : eInfo))

    const sortByLen = (ends: ('start' | 'end')[]) =>
      [...ends].sort((a, b) => {
        const la = a === 'start' ? sLen : eLen
        const lb = b === 'start' ? sLen : eLen
        return config.preferLong ? lb - la : la - lb
      })

    const headEnds = [...sortByLen(preferred), ...sortByLen(fallback)]
    let assigned = false

    for (const headEnd of headEnds) {
      const info = headEnd === 'start' ? sInfo : eInfo

      if (info.trajectory.length === 0) {
        for (const v of path) occupiedVerts.add(v)
        for (let j = 0; j < path.length - 1; j++) occupiedEdges.add(edgeKey(path[j], path[j + 1]))
        placed.push({ id: uuid(), path, headEnd, headDir: computeHeadDir(path, headEnd, geometry.gridCoords) })
        assigned = true; break
      }

      const headV = headEnd === 'end' ? path[path.length - 1] : path[0]
      let prevV = headV, blocked = false
      for (const v of info.trajectory) {
        if (occupiedVerts.has(v) || occupiedEdges.has(edgeKey(prevV, v))) { blocked = true; break }
        prevV = v
      }
      if (!blocked) {
        for (const v of path) occupiedVerts.add(v)
        for (let j = 0; j < path.length - 1; j++) occupiedEdges.add(edgeKey(path[j], path[j + 1]))
        placed.push({ id: uuid(), path, headEnd, headDir: computeHeadDir(path, headEnd, geometry.gridCoords) })
        assigned = true; break
      }
    }

    if (!assigned) return null
  }

  return placed
}

// Fallback: assign any geometric exit, ignoring cross-arrow blocking
function fallbackAssign(
  paths: number[][],
  geometry: CubeGeometry,
  gridSize: GridSize
): Arrow[] {
  return paths.map(path => {
    const s = computeExitInfo(path, 'start', geometry, gridSize)
    const e = computeExitInfo(path, 'end',   geometry, gridSize)
    const sOk = s.exitsCleanly && !s.selfBlocked
    const eOk = e.exitsCleanly && !e.selfBlocked
    const headEnd: 'start' | 'end' =
      sOk && (!eOk || s.trajectory.length <= e.trajectory.length) ? 'start' : 'end'
    return { id: uuid(), path, headEnd, headDir: computeHeadDir(path, headEnd, geometry.gridCoords) }
  })
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
