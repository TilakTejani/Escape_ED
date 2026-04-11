export interface Arrow {
  id: string
  path: number[] // sequence of vertex indices
  headEnd: 'start' | 'end'
  headDir: [number, number, number] // unit direction the arrowhead points (grid space)
}

export interface GridSize {
  x: number
  y: number
  z: number
}

export interface Level {
  gridSize: GridSize
  arrows: Arrow[]
}

export type EditorMode = 'add' | 'select' | 'test'
export type Difficulty = 'easy' | 'medium' | 'hard'

export interface CubeGeometry {
  vertices: [number, number, number][]
  edges: [number, number][]
  adjSet: Set<string> // "min-max" edge keys for O(1) adjacency lookup
}

export interface SavedLevel {
  id: string
  name: string
  savedAt: number // timestamp
  level: Level
}


