import { create } from 'zustand'
import { Arrow, Level, EditorMode, GridSize, Difficulty } from '@/types'
import { areAdjacent, arrowsDirectlyFacing, arrowPointsAtItself, canArrowExit, edgeKey, generateCubeGeometry, getOccupiedEdges } from '@/lib/cube'
import { autoGenerateLevel } from '@/lib/generator'
import { v4 as uuid } from 'uuid'

interface LevelStore {
  gridSize: GridSize
  arrows: Arrow[]
  selectedArrowId: string | null
  mode: EditorMode

  // "Add" mode: building a path
  pendingPath: number[]
  pendingHeadEnd: 'start' | 'end'

  removedInTest: string[]
  hideBlocked: boolean
  straightness: number // 0.0 (turned) to 1.0 (straight)

  // Validation feedback
  pendingError: string | null
  clearPendingError: () => void

  // Actions
  setGridSize: (g: GridSize) => void
  setMode: (mode: EditorMode) => void
  setStraightness: (s: number) => void
  toggleHideBlocked: () => void

  addVertexToPending: (vertexIndex: number) => void
  setPendingHeadEnd: (end: 'start' | 'end') => void
  confirmArrow: () => void
  cancelPending: () => void

  selectArrow: (id: string | null) => void
  deleteArrow: (id: string) => void

  tapArrow: (id: string) => void
  tapFirstRemovable: () => boolean   // taps first exitable arrow; returns false if none left
  resetTest: () => void

  generateArrows: (maxPathLen: number, difficulty: Difficulty) => void

  clearAll: () => void
  importLevel: (level: Level) => void
  exportLevel: () => Level
}

export const useLevelStore = create<LevelStore>((set, get) => ({
  gridSize: { x: 3, y: 3, z: 3 },
  arrows: [],
  selectedArrowId: null,
  mode: 'add',
  straightness: 0.5,
  pendingPath: [],
  pendingHeadEnd: 'end',
  removedInTest: [],
  hideBlocked: false,
  pendingError: null,

  setGridSize: (g) => set({ gridSize: g, arrows: [], pendingPath: [], selectedArrowId: null }),

  setMode: (mode) => set({
    mode,
    pendingPath: [],
    selectedArrowId: null,
    removedInTest: mode === 'test' ? [] : get().removedInTest,
    hideBlocked: false,
  }),

  setStraightness: (s) => set({ straightness: s }),

  toggleHideBlocked: () => set((s) => ({ hideBlocked: !s.hideBlocked })),

  addVertexToPending: (vertexIndex) => {
    const { pendingPath, gridSize, arrows } = get()
    const geometry = generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z)
    const occupied = getOccupiedEdges(arrows)

    // Vertex must not already belong to any existing arrow
    const occupiedVertices = new Set(arrows.flatMap((a) => a.path))

    if (pendingPath.length === 0) {
      if (occupiedVertices.has(vertexIndex)) return
      set({ pendingPath: [vertexIndex] })
      return
    }

    const lastVertex = pendingPath[pendingPath.length - 1]

    // Can't re-add the same vertex
    if (pendingPath.includes(vertexIndex)) return

    // Target vertex must not belong to an existing arrow
    if (occupiedVertices.has(vertexIndex)) return

    // Must be adjacent
    if (!areAdjacent(geometry.edges, lastVertex, vertexIndex)) return

    // Edge must not be occupied by existing arrows
    const key = edgeKey(lastVertex, vertexIndex)
    if (occupied.has(key)) return

    // Edge must not already be in the pending path
    for (let i = 0; i < pendingPath.length - 1; i++) {
      if (edgeKey(pendingPath[i], pendingPath[i + 1]) === key) return
    }

    set({ pendingPath: [...pendingPath, vertexIndex] })
  },

  setPendingHeadEnd: (end) => set({ pendingHeadEnd: end }),

  confirmArrow: () => {
    const { pendingPath, pendingHeadEnd, arrows, gridSize } = get()
    if (pendingPath.length < 2) return

    const geometry = generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z)
    const newArrow: Arrow = { id: uuid(), path: pendingPath, headEnd: pendingHeadEnd }

    if (arrowPointsAtItself(newArrow, geometry, gridSize)) {
      set({ pendingError: 'Arrow cannot point back at its own path.' })
      return
    }

    for (const existing of arrows) {
      if (arrowsDirectlyFacing(newArrow, existing, geometry, gridSize)) {
        set({ pendingError: 'Two arrowheads cannot directly face each other.' })
        return
      }
    }

    set({ arrows: [...arrows, newArrow], pendingPath: [], pendingError: null })
  },

  cancelPending: () => set({ pendingPath: [], pendingError: null }),

  clearPendingError: () => set({ pendingError: null }),

  selectArrow: (id) => set({ selectedArrowId: id }),

  deleteArrow: (id) =>
    set((state) => ({
      arrows: state.arrows.filter((a) => a.id !== id),
      selectedArrowId: state.selectedArrowId === id ? null : state.selectedArrowId,
    })),

  tapArrow: (id) => {
    const { arrows, removedInTest, gridSize } = get()
    const geometry = generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z)
    const remaining = arrows.filter((a) => !removedInTest.includes(a.id))
    if (!canArrowExit(id, remaining, geometry, gridSize)) return
    set({ removedInTest: [...removedInTest, id] })
  },

  tapFirstRemovable: () => {
    const { arrows, removedInTest, gridSize } = get()
    const geometry = generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z)
    const remaining = arrows.filter((a) => !removedInTest.includes(a.id))
    const target = remaining.find((a) => canArrowExit(a.id, remaining, geometry, gridSize))
    if (!target) return false
    set({ removedInTest: [...removedInTest, target.id] })
    return true
  },

  resetTest: () => set({ removedInTest: [] }),

  generateArrows: (maxPathLen, difficulty) => {
    const { gridSize, straightness } = get()
    const arrows = autoGenerateLevel(gridSize, maxPathLen, difficulty, straightness)
    set({ arrows, pendingPath: [], selectedArrowId: null, removedInTest: [], mode: 'test' })
  },

  clearAll: () => set({ arrows: [], selectedArrowId: null, pendingPath: [] }),

  importLevel: (level) =>
    set({
      gridSize: level.gridSize,
      arrows: level.arrows,
      pendingPath: [],
      selectedArrowId: null,
      removedInTest: [],
    }),

  exportLevel: () => ({
    gridSize: get().gridSize,
    arrows: get().arrows,
  }),
}))
