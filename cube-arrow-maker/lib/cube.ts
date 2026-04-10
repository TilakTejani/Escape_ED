import { Arrow, CubeGeometry } from '@/types'

export function gridKey(x: number, y: number, z: number): string {
  return `${x},${y},${z}`
}

// Generate geometry for an nx×ny×nz grid — SURFACE ONLY.
export function generateCubeGeometry(nx: number, ny: number, nz: number): CubeGeometry {
  const sx = 1.0
  const sy = 1.0
  const sz = 1.0

  const centerX = (nx - 1) / 2
  const centerY = (ny - 1) / 2
  const centerZ = (nz - 1) / 2

  const isSurface = (x: number, y: number, z: number) =>
    x === 0 || x === nx - 1 || y === 0 || y === ny - 1 || z === 0 || z === nz - 1

  const posMap = new Map<string, number>()
  const vertices: [number, number, number][] = []
  const gridCoords: [number, number, number][] = []

  // 1. Generate Surface Vertices
  for (let z = 0; z < nz; z++) {
    for (let y = 0; y < ny; y++) {
      for (let x = 0; x < nx; x++) {
        if (isSurface(x, y, z)) {
          const idx = vertices.length
          posMap.set(gridKey(x, y, z), idx)
          vertices.push([x - centerX, y - centerY, z - centerZ])
          gridCoords.push([x, y, z])
        }
      }
    }
  }

  // 2. Generate Edges between adjacent surface vertices
  const edges: [number, number][] = []
  for (let z = 0; z < nz; z++) {
    for (let y = 0; y < ny; y++) {
      for (let x = 0; x < nx; x++) {
        if (!isSurface(x, y, z)) continue
        const a = posMap.get(gridKey(x, y, z))!
        
        // Check 3 directions for neighbors
        if (x < nx - 1 && isSurface(x + 1, y, z))
          edges.push([a, posMap.get(gridKey(x + 1, y, z))!])
        if (y < ny - 1 && isSurface(x, y + 1, z))
          edges.push([a, posMap.get(gridKey(x, y + 1, z))!])
        if (z < nz - 1 && isSurface(x, y, z + 1))
          edges.push([a, posMap.get(gridKey(x, y, z + 1))!])
      }
    }
  }

  // Build adjacency set for O(1) lookup
  const adjSet = new Set<string>()
  for (let i = 0; i < edges.length; i++) {
    const [a, b] = edges[i]
    adjSet.add(edgeKey(a, b))
  }

  return { vertices, gridCoords, edges, posMap, adjSet }
}

export function areAdjacent(edgesOrAdjSet: [number, number][] | Set<string>, v1: number, v2: number): boolean {
  if (edgesOrAdjSet instanceof Set) {
    return edgesOrAdjSet.has(edgeKey(v1, v2))
  }
  for (let i = 0; i < edgesOrAdjSet.length; i++) {
    const [a, b] = edgesOrAdjSet[i]
    if ((a === v1 && b === v2) || (a === v2 && b === v1)) return true
  }
  return false
}

export function edgeKey(v1: number, v2: number): string {
  return v1 < v2 ? `${v1}-${v2}` : `${v2}-${v1}`
}

// Get the normalised exit direction of an arrow
export function getExitDirection(
  vertices: [number, number, number][],
  path: number[],
  headEnd: 'start' | 'end'
): [number, number, number] {
  if (path.length < 2) return [0, 0, 0]
  const [v1, v2] =
    headEnd === 'end'
      ? [path[path.length - 2], path[path.length - 1]]
      : [path[1], path[0]]
  const [x1, y1, z1] = vertices[v1]
  const [x2, y2, z2] = vertices[v2]
  const dx = x2 - x1, dy = y2 - y1, dz = z2 - z1
  const len = Math.sqrt(dx * dx + dy * dy + dz * dz)
  return [dx / len, dy / len, dz / len]
}

// Check if an arrow can exit — walks the full exit path until leaving the cube.
export function canArrowExit(
  arrowId: string,
  arrows: Arrow[],
  geometry: CubeGeometry,
  gridSize: { x: number, y: number, z: number }
): boolean {
  const { gridCoords, posMap } = geometry
  let arrow: Arrow | undefined
  for (let i = 0; i < arrows.length; i++) {
    if (arrows[i].id === arrowId) {
      arrow = arrows[i]
      break
    }
  }
  if (!arrow || arrow.path.length < 2) return true

  // Derive the grid step vector from integer coordinates
  const [tailV, headV] =
    arrow.headEnd === 'end'
      ? [arrow.path[arrow.path.length - 2], arrow.path[arrow.path.length - 1]]
      : [arrow.path[1], arrow.path[0]]

  const [tx, ty, tz] = gridCoords[tailV]
  const [hx, hy, hz] = gridCoords[headV]
  const dx = hx - tx, dy = hy - ty, dz = hz - tz

  let cx = hx, cy = hy, cz = hz
  let prevV = headV

  const ownVerts = new Set(arrow.path)

  while (true) {
    cx += dx; cy += dy; cz += dz

    // 1. Boundary Check: Out of volume -> SUCCESS
    if (cx < 0 || cx >= gridSize.x || cy < 0 || cy >= gridSize.y || cz < 0 || cz >= gridSize.z) {
      return true
    }

    // 2. Vertex Check: Surface node exists?
    const nextVertex = posMap.get(gridKey(cx, cy, cz))
    
    // 3. Core Check: Inside volume but no node? -> BLOCKED BY SOLID CORE
    if (nextVertex === undefined) {
      return false
    }

    // 4. Collision Check: Arrow body obstruction
    if (ownVerts.has(nextVertex)) return false
    
    for (let i = 0; i < arrows.length; i++) {
      const other = arrows[i]
      if (other.id === arrowId) continue
      
      const p = other.path
      // Body check
      for (let j = 0; j < p.length; j++) {
        if (p[j] === nextVertex) return false
      }
      
      // Edge check
      for (let j = 0; j < p.length - 1; j++) {
        const u = p[j], v = p[j+1]
        if ((u === prevV && v === nextVertex) || (v === prevV && u === nextVertex)) {
          return false
        }
      }
    }

    prevV = nextVertex
  }
}

// All edges occupied by the given arrows
export function getOccupiedEdges(arrows: Arrow[]): Set<string> {
  const occupied = new Set<string>()
  for (let j = 0; j < arrows.length; j++) {
    const arrow = arrows[j]
    for (let i = 0; i < arrow.path.length - 1; i++) {
      occupied.add(edgeKey(arrow.path[i], arrow.path[i + 1]))
    }
  }
  return occupied
}

// Walk an arrow's exit path and return every vertex index it passes through
export function exitPathVertices(
  arrow: Arrow,
  geometry: CubeGeometry,
  gridSize: { x: number, y: number, z: number }
): number[] {
  const { gridCoords, posMap } = geometry
  if (arrow.path.length < 2) return []
  const [tailV, headV] =
    arrow.headEnd === 'end'
      ? [arrow.path[arrow.path.length - 2], arrow.path[arrow.path.length - 1]]
      : [arrow.path[1], arrow.path[0]]

  const [tx, ty, tz] = gridCoords[tailV]
  const [hx, hy, hz] = gridCoords[headV]
  const dx = hx - tx, dy = hy - ty, dz = hz - tz
  
  let cx = hx, cy = hy, cz = hz
  const result: number[] = []
  
  while (true) {
    cx += dx; cy += dy; cz += dz
    
    // Boundary check
    if (cx < 0 || cx >= gridSize.x || cy < 0 || cy >= gridSize.y || cz < 0 || cz >= gridSize.z) {
      break
    }
    
    const next = posMap.get(gridKey(cx, cy, cz))
    if (next === undefined) break // Blocked by core or no node
    
    result.push(next)
  }
  return result
}

// Returns true if the arrow's own exit path re-enters any vertex of its own path
export function arrowPointsAtItself(
  arrow: Arrow,
  geometry: CubeGeometry,
  gridSize: { x: number, y: number, z: number }
): boolean {
  const ownVerts = new Set(arrow.path)
  const path = exitPathVertices(arrow, geometry, gridSize)
  for (let i = 0; i < path.length; i++) {
    if (ownVerts.has(path[i])) return true
  }
  return false
}

// Returns true if a1's head is on a2's exit path AND a2's head is on a1's exit path
export function arrowsDirectlyFacing(
  a1: Arrow,
  a2: Arrow,
  geometry: CubeGeometry,
  gridSize: { x: number, y: number, z: number }
): boolean {
  const head1 = a1.headEnd === 'end' ? a1.path[a1.path.length - 1] : a1.path[0]
  const head2 = a2.headEnd === 'end' ? a2.path[a2.path.length - 1] : a2.path[0]
  
  const exit1 = exitPathVertices(a1, geometry, gridSize)
  const exit2 = exitPathVertices(a2, geometry, gridSize)
  
  let head2OnExit1 = false
  for (let i = 0; i < exit1.length; i++) if (exit1[i] === head2) head2OnExit1 = true
  
  let head1OnExit2 = false
  for (let i = 0; i < exit2.length; i++) if (exit2[i] === head1) head1OnExit2 = true
  
  return head2OnExit1 && head1OnExit2
}

// Vertices directly connected to a given vertex
export function getNeighbors(edges: [number, number][], vertex: number): number[] {
  const neighbors: number[] = []
  for (let i = 0; i < edges.length; i++) {
    const [a, b] = edges[i]
    if (a === vertex) neighbors.push(b)
    else if (b === vertex) neighbors.push(a)
  }
  return neighbors
}
