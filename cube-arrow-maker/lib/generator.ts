import { Arrow, GridSize, Difficulty, CubeGeometry } from '@/types'
import { generateCubeGeometry, getNextStraightVertex, getExitDirection } from './cube'
import { v4 as uuid } from 'uuid'

export function computeHeadDir(
  path: number[],
  headEnd: 'start' | 'end',
  geometry: CubeGeometry
): [number, number, number] {
  return getExitDirection(geometry.vertices, path, headEnd, geometry.edges)
}

// Max number of face-boundary crossings allowed per arrow path.
// easy   = stays on one face (0 crossings)
// medium = can cross one edge onto an adjacent face (1 crossing)
// hard   = no restriction
const MAX_FACE_CHANGES: Record<Difficulty, number> = {
  easy:   0,
  medium: 1,
  hard:   Infinity,
}

function getFaceAxis(x: number, y: number, z: number): 'x' | 'y' | 'z' {
  const ax = Math.abs(x), ay = Math.abs(y), az = Math.abs(z)
  if (ax > ay && ax > az) return 'x'
  if (ay > ax && ay > az) return 'y'
  return 'z'
}

export function autoGenerateLevel(
  gridSize: GridSize,
  userMaxLen: number,
  difficulty: Difficulty,
  straightnessParam: number = 0.5
): Arrow[] {
  const geometry = generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z)
  const targetMaxLen = Math.max(2, userMaxLen)

  let bestArrows: Arrow[] = []

  for (let attempt = 0; attempt < 50; attempt++) {
    const arrows = greedyReverseGenerate(geometry, gridSize, targetMaxLen, difficulty, straightnessParam)
    if (arrows.length > bestArrows.length) {
      bestArrows = arrows
    }
  }

  return bestArrows
}

function greedyReverseGenerate(
  geometry: CubeGeometry,
  gridSize: GridSize,
  maxLen: number,
  difficulty: Difficulty,
  straightnessParam: number,
): Arrow[] {
  const numV = geometry.vertices.length
  const { edges, vertices } = geometry
  const maxFaceChanges = MAX_FACE_CHANGES[difficulty]

  const adj: number[][] = Array.from({ length: numV }, () => [])
  for (let i = 0; i < edges.length; i++) {
    const [u, v] = edges[i]
    adj[u].push(v)
    adj[v].push(u)
  }

  const occupied = new Uint8Array(numV)
  const occupiedEdges = new Set<string>()
  const placedArrows: Arrow[] = []

  function vertexFaceAxis(vi: number): 'x' | 'y' | 'z' {
    const [x, y, z] = vertices[vi]
    return getFaceAxis(x, y, z)
  }

  // Mirrors exitPathVertices: walks straight from headV away from tailV,
  // stopping at face-boundary crossings (fly-off = clear) or step limit.
  // Bug fix: the old version did NOT stop at face changes, so it walked onto
  // adjacent faces and found occupied vertices there — falsely blocking placements.
  function hasClearExit(headV: number, tailV: number): boolean {
    let u = tailV
    let v = headV
    let steps = 0
    const limit = Math.max(gridSize.x, gridSize.y, gridSize.z) + 1
    while (steps < limit) {
      const nextV = getNextStraightVertex(u, v, vertices, edges)
      if (nextV === null) return true

      // Fly-off: arrow leaves the face surface → it's free in 3D space
      if (vertexFaceAxis(v) !== vertexFaceAxis(nextV)) return true

      if (occupied[nextV]) return false
      const a = v < nextV ? v : nextV
      const b = v < nextV ? nextV : v
      if (occupiedEdges.has(`${a}-${b}`)) return false

      u = v
      v = nextV
      steps++
    }
    return true
  }

  while (true) {
    // Most-constrained-first: pick the unoccupied vertex with fewest free neighbors.
    let bestHead = -1
    let minEmptyNeighbors = 999

    for (let i = 0; i < numV; i++) {
      if (occupied[i]) continue
      let emptyCount = 0
      for (let k = 0; k < adj[i].length; k++) {
        if (!occupied[adj[i][k]]) emptyCount++
      }
      if (emptyCount < minEmptyNeighbors) {
        minEmptyNeighbors = emptyCount
        bestHead = i
      } else if (emptyCount === minEmptyNeighbors && Math.random() < 0.2) {
        bestHead = i
      }
    }

    if (bestHead === -1) break

    const head = bestHead
    const possibleExits = shuffled(adj[head])
    let placed = false

    for (let i = 0; i < possibleExits.length; i++) {
      const nb = possibleExits[i]
      if (occupied[nb]) continue

      // Difficulty gate on first step: easy arrows must start on the same face
      const firstFaceChange = vertexFaceAxis(head) !== vertexFaceAxis(nb) ? 1 : 0
      if (firstFaceChange > maxFaceChanges) continue

      if (!hasClearExit(head, nb)) continue

      const path: number[] = [head, nb]
      const pathSet = new Set<number>([head, nb])
      let current = nb
      let pathLen = 2
      let faceChanges = firstFaceChange

      const currentMaxLen = Math.min(maxLen, 2 + Math.floor(Math.random() * (maxLen - 1)))

      while (pathLen < currentMaxLen) {
        let extendTo = -1
        let extendFaceChanges = faceChanges

        // Bias toward straight continuation based on straightnessParam
        if (straightnessParam > 0 && Math.random() < straightnessParam) {
          const straight = getNextStraightVertex(path[pathLen - 2], current, vertices, edges)
          if (straight !== null && !occupied[straight] && !pathSet.has(straight)) {
            const newChanges = vertexFaceAxis(current) !== vertexFaceAxis(straight)
              ? faceChanges + 1
              : faceChanges
            if (newChanges <= maxFaceChanges) {
              extendTo = straight
              extendFaceChanges = newChanges
            }
          }
        }

        // Fall back to a random unoccupied neighbor within difficulty constraint
        if (extendTo === -1) {
          const candidates = shuffled(adj[current])
          for (let j = 0; j < candidates.length; j++) {
            const c = candidates[j]
            if (occupied[c] || pathSet.has(c)) continue
            const newChanges = vertexFaceAxis(current) !== vertexFaceAxis(c)
              ? faceChanges + 1
              : faceChanges
            if (newChanges > maxFaceChanges) continue
            extendTo = c
            extendFaceChanges = newChanges
            break
          }
        }

        if (extendTo === -1) break

        path.push(extendTo)
        pathSet.add(extendTo)
        current = extendTo
        faceChanges = extendFaceChanges
        pathLen++
      }

      for (let j = 0; j < path.length; j++) occupied[path[j]] = 1
      for (let j = 0; j < path.length - 1; j++) {
        const u = path[j], v = path[j + 1]
        const a = u < v ? u : v
        const b = u < v ? v : u
        occupiedEdges.add(`${a}-${b}`)
      }

      placedArrows.push({
        id: uuid(),
        path,
        headEnd: 'start',
        headDir: computeHeadDir(path, 'start', geometry),
      })
      placed = true
      break
    }

    if (!placed) {
      // No valid head placement found — mark as skip so the loop terminates
      occupied[head] = 1
    }
  }

  return shuffled(placedArrows)
}

function shuffled<T>(arr: T[]): T[] {
  const a = [...arr]
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[a[i], a[j]] = [a[j], a[i]]
  }
  return a
}
