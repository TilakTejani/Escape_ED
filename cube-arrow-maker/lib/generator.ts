import { Arrow, GridSize, Difficulty, CubeGeometry } from '@/types'
import { generateCubeGeometry, getExitDirection, exitPathVertices, arrowPointsAtItself, edgeKey } from './cube'
import { v4 as uuid } from 'uuid'

// ─── Difficulty config ────────────────────────────────────────────────────────

interface DifficultyConfig {
  facePenalty: number       // scoring cost per new face entered; high = stay on one face
  preferredMaxTraj: number  // desired max exit-trajectory tile count
  preferLong: boolean       // hard: assign longest-trajectory ends first
}

const DIFFICULTY_CONFIG: Record<Difficulty, DifficultyConfig> = {
  easy:   { facePenalty: 8,   preferredMaxTraj: 1,   preferLong: false },
  medium: { facePenalty: 2,   preferredMaxTraj: 3,   preferLong: false },
  hard:   { facePenalty: 0,   preferredMaxTraj: 999, preferLong: true  },
}

// ─── Face masks (1 bit per face; each tile center is on exactly one face) ─────
// Bit 0=+X  1=−X  2=+Y  3=−Y  4=+Z  5=−Z

function computeFaceMasks(vertices: [number, number, number][], gridSize: GridSize): number[] {
  const Rx = gridSize.x / 2, Ry = gridSize.y / 2, Rz = gridSize.z / 2
  const EPS = 0.01
  return vertices.map(([x, y, z]) => {
    if (Math.abs(x - Rx) < EPS) return 1
    if (Math.abs(x + Rx) < EPS) return 2
    if (Math.abs(y - Ry) < EPS) return 4
    if (Math.abs(y + Ry) < EPS) return 8
    if (Math.abs(z - Rz) < EPS) return 16
    if (Math.abs(z + Rz) < EPS) return 32
    return 0
  })
}

function popcount(n: number): number {
  let c = 0; while (n) { c += n & 1; n >>>= 1 } return c
}

// ─── Phase 1: buildPaths ──────────────────────────────────────────────────────

function buildPaths(
  geometry: CubeGeometry,
  gridSize: GridSize,
  maxLen: number,
  config: DifficultyConfig,
  straightness: number
): number[][] {
  const { vertices, edges } = geometry
  const numV = vertices.length
  const faceMasks = computeFaceMasks(vertices, gridSize)

  const adj: number[][] = Array.from({ length: numV }, () => [])
  for (const [u, v] of edges) { adj[u].push(v); adj[v].push(u) }

  const visited = new Uint8Array(numV)
  const freeDeg = new Int32Array(numV)
  for (let i = 0; i < numV; i++) freeDeg[i] = adj[i].length

  const paths: number[][] = []

  function markVisited(v: number) {
    if (visited[v]) return
    visited[v] = 1
    for (const nb of adj[v]) if (!visited[nb]) freeDeg[nb]--
  }

  function chooseStart(): number {
    let best = -1, bestScore = Infinity
    for (let i = 0; i < numV; i++) {
      if (visited[i]) continue
      const score = freeDeg[i] <= 1 ? freeDeg[i] : freeDeg[i] + Math.random() * 5
      if (score < bestScore) { bestScore = score; best = i }
    }
    return best
  }

  function scoreStep(nb: number, pathMask: number, lastDir: [number,number,number] | null, curV: number): number {
    const newFaces = popcount(faceMasks[nb] & ~pathMask)
    let score = freeDeg[nb] + newFaces * config.facePenalty * 0.5 + Math.random() * 3.0
    if (lastDir) {
      const [cx, cy, cz] = vertices[curV], [nx, ny, nz] = vertices[nb]
      const dot = lastDir[0]*(nx-cx) + lastDir[1]*(ny-cy) + lastDir[2]*(nz-cz)
      if (dot > 0.5) score -= (straightness - 0.5) * 6
    }
    return score
  }

  // Rescue isolated vertex u by grafting onto a committed path
  function rescueVertex(u: number): boolean {
    for (let pi = 0; pi < paths.length; pi++) {
      const p = paths[pi]
      // Tail extension
      if (adj[p[p.length - 1]].includes(u)) { p.push(u); markVisited(u); return true }
      // Head reversal + extension
      if (adj[p[0]].includes(u)) { p.reverse(); p.push(u); markVisited(u); return true }
      // Middle split: u adjacent to interior vertex m
      for (let mi = 1; mi < p.length - 1; mi++) {
        if (adj[p[mi]].includes(u)) {
          const right = p.splice(mi + 1)  // p is now p[0..mi]
          p.push(u)
          markVisited(u)
          if (right.length >= 2) paths.push(right)
          return true
        }
      }
    }
    return false
  }

  while (true) {
    const start = chooseStart()
    if (start === -1) break

    const path: number[] = [start]
    let pathMask = faceMasks[start]
    markVisited(start)

    for (const nb of adj[start]) {
      if (!visited[nb] && freeDeg[nb] === 0) rescueVertex(nb)
    }

    let lastDir: [number,number,number] | null = null
    let curV = start

    while (path.length < maxLen) {
      const candidates = adj[curV].filter(v => !visited[v])
      if (candidates.length === 0) break

      let best = -1, bestScore = Infinity
      for (const nb of candidates) {
        const s = scoreStep(nb, pathMask, lastDir, curV)
        if (s < bestScore) { bestScore = s; best = nb }
      }

      const [cx, cy, cz] = vertices[curV], [bx, by, bz] = vertices[best]
      lastDir = [bx-cx, by-cy, bz-cz]
      pathMask |= faceMasks[best]
      path.push(best)
      markVisited(best)

      for (const nb of adj[best]) {
        if (!visited[nb] && freeDeg[nb] === 0) rescueVertex(nb)
      }

      curV = best
    }

    if (path.length >= 2) paths.push(path)
  }

  return paths
}

// ─── Phase 2: assignExits ─────────────────────────────────────────────────────

interface ExitInfo {
  end: 'start' | 'end'
  selfBlocked: boolean
  trajectory: number[]
}

function computeExitInfo(path: number[], end: 'start' | 'end', geometry: CubeGeometry, gridSize: GridSize): ExitInfo {
  const tempArrow: Arrow = {
    id: '__temp__',
    path,
    headEnd: end,
    headDir: getExitDirection(geometry.vertices, path, end, geometry.edges),
  }
  return {
    end,
    selfBlocked: arrowPointsAtItself(tempArrow, geometry, gridSize),
    trajectory: exitPathVertices(tempArrow, geometry, gridSize),
  }
}

function assignExits(
  paths: number[][],
  geometry: CubeGeometry,
  gridSize: GridSize,
  config: DifficultyConfig
): Arrow[] | null {
  const exitInfos: [ExitInfo, ExitInfo][] = paths.map(p => [
    computeExitInfo(p, 'start', geometry, gridSize),
    computeExitInfo(p, 'end',   geometry, gridSize),
  ])

  // Sort by min valid trajectory length (most constrained first)
  const order = paths.map((_, i) => i).sort((a, b) => {
    const va = exitInfos[a].filter(e => !e.selfBlocked)
    const vb = exitInfos[b].filter(e => !e.selfBlocked)
    const minA = va.length > 0 ? Math.min(...va.map(e => e.trajectory.length)) : 999
    const minB = vb.length > 0 ? Math.min(...vb.map(e => e.trajectory.length)) : 999
    return config.preferLong ? minB - minA : minA - minB
  })

  const placedVerts = new Set<number>()
  const placedEdges = new Set<string>()
  const result: Arrow[] = []

  for (const pi of order) {
    const path = paths[pi]
    let candidates = exitInfos[pi].filter(e => !e.selfBlocked)
    if (candidates.length === 0) return null

    const preferred = candidates.filter(e => e.trajectory.length <= config.preferredMaxTraj)
    if (preferred.length > 0) candidates = preferred

    let chosen: ExitInfo | null = null
    for (const cand of candidates) {
      let clear = true
      let prevV = cand.end === 'end' ? path[path.length - 1] : path[0]
      for (const tv of cand.trajectory) {
        if (placedVerts.has(tv) || placedEdges.has(edgeKey(prevV, tv))) { clear = false; break }
        prevV = tv
      }
      if (clear) { chosen = cand; break }
    }
    if (!chosen) chosen = candidates[0]  // fallback: ignore cross-arrow blocking

    for (const v of path) placedVerts.add(v)
    for (let i = 0; i < path.length - 1; i++) placedEdges.add(edgeKey(path[i], path[i+1]))

    result.push({
      id: uuid(),
      path,
      headEnd: chosen.end,
      headDir: getExitDirection(geometry.vertices, path, chosen.end, geometry.edges),
    })
  }

  return result
}

// ─── Entry point ──────────────────────────────────────────────────────────────

export function autoGenerateLevel(
  gridSize: GridSize,
  userMaxLen: number,
  difficulty: Difficulty,
  straightness = 0.5
): Arrow[] {
  const config = DIFFICULTY_CONFIG[difficulty]
  const geometry = generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z)
  const maxLen = Math.max(2, userMaxLen)

  let bestArrows: Arrow[] = []

  for (let attempt = 0; attempt < 20; attempt++) {
    const paths = buildPaths(geometry, gridSize, maxLen, config, straightness)
    const arrows = assignExits(paths, geometry, gridSize, config)
    if (arrows && arrows.length > bestArrows.length) bestArrows = arrows
  }

  // Fallback: assign all headEnd:'start'
  if (bestArrows.length === 0) {
    const paths = buildPaths(geometry, gridSize, maxLen, config, straightness)
    bestArrows = paths.map(path => ({
      id: uuid(),
      path,
      headEnd: 'start' as const,
      headDir: getExitDirection(geometry.vertices, path, 'start', geometry.edges),
    }))
  }

  return bestArrows
}
