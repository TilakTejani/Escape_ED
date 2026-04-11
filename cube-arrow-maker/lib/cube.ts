import { Arrow, CubeGeometry } from '@/types'

export const worldKey = (x: number, y: number, z: number) => `${x.toFixed(2)},${y.toFixed(2)},${z.toFixed(2)}`

export function generateCubeGeometry(nx: number, ny: number, nz: number): CubeGeometry {
  const vertices: [number, number, number][] = []
  
  const posMap = new Map<string, number>()
  
  const addVertex = (x: number, y: number, z: number) => {
    const idx = vertices.length;
    vertices.push([x, y, z])
    posMap.set(worldKey(x, y, z), idx)
    return idx
  }

  const Rx = nx / 2;
  const Ry = ny / 2;
  const Rz = nz / 2;

  // Front (+Z) & Back (-Z)
  for (let j = 0; j < ny; j++) {
    for (let i = 0; i < nx; i++) {
        const c_x = i - (nx - 1) / 2;
        const c_y = j - (ny - 1) / 2;
        addVertex(c_x, c_y, Rz);  // Front
        addVertex(-c_x, c_y, -Rz); // Back
    }
  }

  // Top (+Y) & Bottom (-Y)
  for (let j = 0; j < nz; j++) {
    for (let i = 0; i < nx; i++) {
        const c_x = i - (nx - 1) / 2;
        const c_z = j - (nz - 1) / 2;
        addVertex(c_x, Ry, -c_z); // Top
        addVertex(c_x, -Ry, c_z); // Bottom
    }
  }

  // Right (+X) & Left (-X)
  for (let j = 0; j < ny; j++) {
    for (let i = 0; i < nz; i++) {
        const c_z = i - (nz - 1) / 2;
        const c_y = j - (ny - 1) / 2;
        addVertex(Rx, c_y, -c_z); // Right
        addVertex(-Rx, c_y, c_z); // Left
    }
  }

  const edges: [number, number][] = []
  const adjSet = new Set<string>()
  
  const addEdge = (a: number, b: number) => {
    const key = a < b ? `${a}-${b}` : `${b}-${a}`
    if (!adjSet.has(key)) {
      edges.push([a, b])
      adjSet.add(key)
    }
  }

  // Pass 1: same-face edges via unit-offset neighbor lookup (O(V))
  const OFFSETS: [number, number, number][] = [
    [1, 0, 0], [-1, 0, 0],
    [0, 1, 0], [0, -1, 0],
    [0, 0, 1], [0, 0, -1],
  ]
  for (let i = 0; i < vertices.length; i++) {
    const [x, y, z] = vertices[i]
    for (let o = 0; o < OFFSETS.length; o++) {
      const [dx, dy, dz] = OFFSETS[o]
      const j = posMap.get(worldKey(x + dx, y + dy, z + dz))
      if (j !== undefined && j > i) addEdge(i, j)
    }
  }

  // Pass 2: cross-face edges between border tiles of adjacent faces.
  // Each vertex is a tile center on exactly one face. Tile centers on
  // adjacent faces are ~0.707 apart (not 1.0), so pass 1 misses them.
  // The border tile of face A connects to the border tile of the adjacent
  // face B that shares the same coordinate along the shared-edge axis.
  //
  // Example (3×3×3): front-face top-row tile (x, 1, 1.5) ↔ top-face front-row tile (x, 1.5, 1)
  const EPS = 0.01
  for (let i = 0; i < vertices.length; i++) {
    const [ax, ay, az] = vertices[i]
    const onPZ = Math.abs(az - Rz) < EPS
    const onNZ = Math.abs(az + Rz) < EPS
    const onPY = Math.abs(ay - Ry) < EPS
    const onNY = Math.abs(ay + Ry) < EPS
    const onPX = Math.abs(ax - Rx) < EPS
    const onNX = Math.abs(ax + Rx) < EPS

    if (onPZ || onNZ) {
      const s = onPZ ? 1 : -1
      const bz = s * (Rz - 0.5)
      if (Math.abs(ay - (Ry - 0.5)) < EPS) { const j = posMap.get(worldKey(ax,  Ry, bz)); if (j !== undefined) addEdge(i, j) }
      if (Math.abs(ay + (Ry - 0.5)) < EPS) { const j = posMap.get(worldKey(ax, -Ry, bz)); if (j !== undefined) addEdge(i, j) }
      if (Math.abs(ax - (Rx - 0.5)) < EPS) { const j = posMap.get(worldKey( Rx, ay, bz)); if (j !== undefined) addEdge(i, j) }
      if (Math.abs(ax + (Rx - 0.5)) < EPS) { const j = posMap.get(worldKey(-Rx, ay, bz)); if (j !== undefined) addEdge(i, j) }
    }
    if (onPY || onNY) {
      const s = onPY ? 1 : -1
      const by = s * (Ry - 0.5)
      if (Math.abs(az - (Rz - 0.5)) < EPS) { const j = posMap.get(worldKey(ax, by,  Rz)); if (j !== undefined) addEdge(i, j) }
      if (Math.abs(az + (Rz - 0.5)) < EPS) { const j = posMap.get(worldKey(ax, by, -Rz)); if (j !== undefined) addEdge(i, j) }
      if (Math.abs(ax - (Rx - 0.5)) < EPS) { const j = posMap.get(worldKey( Rx, by, az)); if (j !== undefined) addEdge(i, j) }
      if (Math.abs(ax + (Rx - 0.5)) < EPS) { const j = posMap.get(worldKey(-Rx, by, az)); if (j !== undefined) addEdge(i, j) }
    }
    if (onPX || onNX) {
      const s = onPX ? 1 : -1
      const bx = s * (Rx - 0.5)
      if (Math.abs(ay - (Ry - 0.5)) < EPS) { const j = posMap.get(worldKey(bx,  Ry, az)); if (j !== undefined) addEdge(i, j) }
      if (Math.abs(ay + (Ry - 0.5)) < EPS) { const j = posMap.get(worldKey(bx, -Ry, az)); if (j !== undefined) addEdge(i, j) }
      if (Math.abs(az - (Rz - 0.5)) < EPS) { const j = posMap.get(worldKey(bx, ay,  Rz)); if (j !== undefined) addEdge(i, j) }
      if (Math.abs(az + (Rz - 0.5)) < EPS) { const j = posMap.get(worldKey(bx, ay, -Rz)); if (j !== undefined) addEdge(i, j) }
    }
  }

  return { vertices, edges, adjSet }
}

export function areAdjacent(adjSet: Set<string>, v1: number, v2: number): boolean {
  return adjSet.has(edgeKey(v1, v2))
}

export function edgeKey(v1: number, v2: number): string {
  return v1 < v2 ? `${v1}-${v2}` : `${v2}-${v1}`
}

export function getNeighbors(edges: [number, number][], vertex: number): number[] {
  const neighbors: number[] = []
  for (let i = 0; i < edges.length; i++) {
    const [a, b] = edges[i]
    if (a === vertex) neighbors.push(b)
    else if (b === vertex) neighbors.push(a)
  }
  return neighbors
}

export function getOccupiedVertices(arrows: Arrow[]): Set<number> {
  const occupied = new Set<number>()
  for (let i = 0; i < arrows.length; i++) {
    const path = arrows[i].path
    for (let j = 0; j < path.length; j++) occupied.add(path[j])
  }
  return occupied
}

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

// Get the geometrically straight next step across the faces.
// Solves 90-degree corner wrapping perfectly by selecting the highest positive dot product!
export function getNextStraightVertex(u: number, v: number, vertices: [number,number,number][], edges: [number,number][]): number | null {
   const [ux, uy, uz] = vertices[u]
   const [vx, vy, vz] = vertices[v]
   const dirX = vx - ux, dirY = vy - uy, dirZ = vz - uz
   
   let bestNext = -1;
   let maxDot = -999;
   
   const neighbors = getNeighbors(edges, v);
   for (let i = 0; i < neighbors.length; i++) {
      const w = neighbors[i]
      if (w === u) continue; 
      
      const [wx, wy, wz] = vertices[w]
      const dx = wx - vx, dy = wy - vy, dz = wz - vz
      
      const dot = dirX * dx + dirY * dy + dirZ * dz
      if (dot > maxDot) {
          maxDot = dot;
          bestNext = w;
      }
   }
   
   if (maxDot > 0.1) return bestNext;
   return null;
}

export function getExitDirection(
  vertices: [number, number, number][],
  path: number[],
  headEnd: 'start' | 'end',
  _edges: [number, number][]
): [number, number, number] {
  if (path.length < 2) return [0, 0, 0]
  const [v1, v2] =
    headEnd === 'end'
      ? [path[path.length - 2], path[path.length - 1]]
      : [path[1], path[0]]
      
  const [nx, ny, nz] = vertices[v1]   // Neck
  const [hx, hy, hz] = vertices[v2]   // Head
  
  let dx = hx - nx
  let dy = hy - ny
  let dz = hz - nz
  
  // Find the exact normal of the face the Head sits on.
  // Because tiles are flat on faces, exactly ONE coordinate corresponds to the cube radius R.
  // All other coordinates are strictly less than R (maximum R - 0.5).
  const ax = Math.abs(hx)
  const ay = Math.abs(hy)
  const az = Math.abs(hz)
  
  if (ax > ay && ax > az) {
      dx = 0; // Project onto YZ plane
  } else if (ay > ax && ay > az) {
      dy = 0; // Project onto XZ plane
  } else {
      dz = 0; // Project onto XY plane
  }
  
  const len = Math.sqrt(dx * dx + dy * dy + dz * dz)
  if (len < 0.0001) return [0, 0, 1] 
  return [dx / len, dy / len, dz / len]
}

function getFaceNormalAxis(vx: number, vy: number, vz: number): 'x' | 'y' | 'z' {
  const ax = Math.abs(vx), ay = Math.abs(vy), az = Math.abs(vz)
  if (ax > ay && ax > az) return 'x'
  if (ay > ax && ay > az) return 'y'
  return 'z'
}

export function exitPathVertices(
  arrow: Arrow,
  geometry: CubeGeometry,
  gridSize: { x: number, y: number, z: number }
): number[] {
  const { vertices, edges } = geometry
  if (arrow.path.length < 2) return []
  
  const [tailV, headV] =
    arrow.headEnd === 'end'
      ? [arrow.path[arrow.path.length - 2], arrow.path[arrow.path.length - 1]]
      : [arrow.path[1], arrow.path[0]]

  const result: number[] = []
  
  let u = tailV
  let v = headV
  
  let steps = 0
  const limit = Math.max(gridSize.x, gridSize.y, gridSize.z) + 1
  while (steps < limit) {
    const nextV = getNextStraightVertex(u, v, vertices, edges)
    if (nextV === null) break
    
    const [vx, vy, vz] = vertices[v]
    const [nx, ny, nz] = vertices[nextV]
    
    const vAxis = getFaceNormalAxis(vx, vy, vz)
    const nextAxis = getFaceNormalAxis(nx, ny, nz)
    
    // Fly-Off Mechanic: If the arrow moves onto a different face, it has flown off the edge into 3D space!
    if (vAxis !== nextAxis) break
    
    result.push(nextV)
    u = v
    v = nextV
    steps++
  }
  
  return result
}

export function canArrowExit(
  arrowId: string,
  arrows: Arrow[],
  geometry: CubeGeometry,
  gridSize: { x: number, y: number, z: number }
): boolean {
  let arrow: Arrow | undefined
  for (let i = 0; i < arrows.length; i++) {
    if (arrows[i].id === arrowId) {
      arrow = arrows[i]
      break
    }
  }
  if (!arrow || arrow.path.length < 2) return true
  
  const path = exitPathVertices(arrow, geometry, gridSize)

  let prevV = arrow.headEnd === 'end' ? arrow.path[arrow.path.length-1] : arrow.path[0]
  
  for (let k = 0; k < path.length; k++) {
      const nextVertex = path[k]
      
      // Collision Check: Self (Snake-like slithering rules)
      // An arrow only collides with itself if its head steps onto a stationary part of its body that hasn't slithered out of the way yet.
      const stepDistance = k + 1
      const originalPath = arrow.headEnd === 'end' ? [...arrow.path].reverse() : arrow.path
      
      // Node self-collision
      for (let s = 0; s < originalPath.length - stepDistance; s++) {
        if (originalPath[s] === nextVertex) return false
      }
      
      // Edge self-collision (cannot cross its own body's current segments)
      for (let s = 0; s < originalPath.length - stepDistance - 1; s++) {
        const a = originalPath[s], b = originalPath[s+1]
        if ((a === prevV && b === nextVertex) || (b === prevV && a === nextVertex)) return false
      }
      
      // Collision Check: Others
      for (let i = 0; i < arrows.length; i++) {
        const other = arrows[i]
        if (other.id === arrowId) continue
        
        const p = other.path
        for (let j = 0; j < p.length; j++) {
          if (p[j] === nextVertex) return false
        }
        
        for (let j = 0; j < p.length - 1; j++) {
          const a = p[j], b = p[j+1]
          if ((a === prevV && b === nextVertex) || (b === prevV && a === nextVertex)) return false
        }
      }
      prevV = nextVertex
  }
  
  return true
}

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
