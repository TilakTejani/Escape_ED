import { create } from 'zustand'
import { Arrow, Level, EditorMode, GridSize, Difficulty, CubeGeometry, SavedLevel } from '@/types'
import { areAdjacent, arrowsDirectlyFacing, arrowPointsAtItself, canArrowExit, edgeKey, generateCubeGeometry, getOccupiedEdges } from '@/lib/cube'
import { autoGenerateLevel } from '@/lib/generator'
import { v4 as uuid } from 'uuid'

const STORAGE_KEY = 'escape-ed-levels'

function loadSavedLevels(): SavedLevel[] {
  if (typeof window === 'undefined') return []
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

function persistSavedLevels(levels: SavedLevel[]) {
  if (typeof window === 'undefined') return
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(levels))
  } catch {
    // quota exceeded or SSR — ignore
  }
}

interface LevelStore {
  gridSize: GridSize
  arrows: Arrow[]
  selectedArrowId: string | null
  mode: EditorMode

  // Cached geometry — recomputed only when gridSize changes
  geometry: CubeGeometry
  // Cached occupied edges — recomputed only when arrows change
  occupiedEdges: Set<string>

  // "Add" mode: building a path
  pendingPath: number[]
  pendingHeadEnd: 'start' | 'end'

  removedInTest: string[]
  hideBlocked: boolean
  straightness: number // 0.0 (turned) to 1.0 (straight)

  // Validation feedback
  pendingError: string | null
  clearPendingError: () => void

  // Level management
  savedLevels: SavedLevel[]
  currentLevelId: string | null
  currentLevelName: string

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
  tapFirstRemovable: () => boolean
  resetTest: () => void

  generateArrows: (maxPathLen: number, difficulty: Difficulty) => boolean

  clearAll: () => void
  importLevel: (level: Level) => void
  exportLevel: () => Level

  // Level management actions
  saveCurrentLevel: (name?: string) => void
  loadLevel: (id: string) => void
  deleteLevel: (id: string) => void
  renameLevel: (id: string, name: string) => void
  newLevel: () => void
  setCurrentLevelName: (name: string) => void
}

const initialGridSize: GridSize = { x: 3, y: 3, z: 3 }
const initialGeometry = generateCubeGeometry(initialGridSize.x, initialGridSize.y, initialGridSize.z)

export const useLevelStore = create<LevelStore>((set, get) => ({
  gridSize: initialGridSize,
  arrows: [],
  selectedArrowId: null,
  mode: 'add',
  straightness: 0.5,
  pendingPath: [],
  pendingHeadEnd: 'end',
  removedInTest: [],
  hideBlocked: false,
  pendingError: null,

  geometry: initialGeometry,
  occupiedEdges: new Set<string>(),

  savedLevels: loadSavedLevels(),
  currentLevelId: null,
  currentLevelName: 'Untitled Level',

  setGridSize: (g) => {
    const geometry = generateCubeGeometry(g.x, g.y, g.z)
    set({ gridSize: g, arrows: [], pendingPath: [], selectedArrowId: null, geometry, occupiedEdges: new Set() })
  },

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
    const { pendingPath, arrows, geometry, occupiedEdges } = get()
    const occupiedVertices = new Set(arrows.flatMap((a) => a.path))

    if (pendingPath.length === 0) {
      if (occupiedVertices.has(vertexIndex)) return
      set({ pendingPath: [vertexIndex] })
      return
    }

    const lastVertex = pendingPath[pendingPath.length - 1]
    if (pendingPath.includes(vertexIndex)) return
    if (occupiedVertices.has(vertexIndex)) return
    if (!areAdjacent(geometry.adjSet, lastVertex, vertexIndex)) return

    const key = edgeKey(lastVertex, vertexIndex)
    if (occupiedEdges.has(key)) return

    for (let i = 0; i < pendingPath.length - 1; i++) {
      if (edgeKey(pendingPath[i], pendingPath[i + 1]) === key) return
    }

    set({ pendingPath: [...pendingPath, vertexIndex] })
  },

  setPendingHeadEnd: (end) => set({ pendingHeadEnd: end }),

  confirmArrow: () => {
    const { pendingPath, pendingHeadEnd, arrows, gridSize, geometry, occupiedEdges } = get()
    if (pendingPath.length < 2) return

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

    const newArrows = [...arrows, newArrow]
    const newOccupied = new Set(occupiedEdges)
    for (let i = 0; i < pendingPath.length - 1; i++) {
      newOccupied.add(edgeKey(pendingPath[i], pendingPath[i + 1]))
    }
    set({ arrows: newArrows, pendingPath: [], pendingError: null, occupiedEdges: newOccupied })
  },

  cancelPending: () => set({ pendingPath: [], pendingError: null }),

  clearPendingError: () => set({ pendingError: null }),

  selectArrow: (id) => set({ selectedArrowId: id }),

  deleteArrow: (id) =>
    set((state) => {
      const newArrows = state.arrows.filter((a) => a.id !== id)
      return {
        arrows: newArrows,
        selectedArrowId: state.selectedArrowId === id ? null : state.selectedArrowId,
        occupiedEdges: getOccupiedEdges(newArrows),
      }
    }),

  tapArrow: (id) => {
    const { arrows, removedInTest, gridSize, geometry } = get()
    const remaining = arrows.filter((a) => !removedInTest.includes(a.id))
    if (!canArrowExit(id, remaining, geometry, gridSize)) return
    set({ removedInTest: [...removedInTest, id] })
  },

  tapFirstRemovable: () => {
    const { arrows, removedInTest, gridSize, geometry } = get()
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
    if (arrows.length === 0) return false  // generation failed — keep existing state
    const occupiedEdges = getOccupiedEdges(arrows)
    set({ arrows, pendingPath: [], selectedArrowId: null, removedInTest: [], mode: 'test', occupiedEdges })
    return true
  },

  clearAll: () => set({ arrows: [], selectedArrowId: null, pendingPath: [], occupiedEdges: new Set() }),

  importLevel: (level) => {
    const geometry = generateCubeGeometry(level.gridSize.x, level.gridSize.y, level.gridSize.z)
    const occupiedEdges = getOccupiedEdges(level.arrows)
    set({
      gridSize: level.gridSize,
      arrows: level.arrows,
      pendingPath: [],
      selectedArrowId: null,
      removedInTest: [],
      geometry,
      occupiedEdges,
    })
  },

  exportLevel: () => ({
    gridSize: get().gridSize,
    arrows: get().arrows,
  }),

  // Level management
  saveCurrentLevel: (name) => {
    const { gridSize, arrows, currentLevelId, currentLevelName, savedLevels } = get()
    const levelName = name ?? currentLevelName
    const level: Level = { gridSize, arrows }

    let newSavedLevels: SavedLevel[]
    let targetId: string

    if (currentLevelId) {
      targetId = currentLevelId
      newSavedLevels = savedLevels.map((sl) =>
        sl.id === currentLevelId
          ? { ...sl, name: levelName, savedAt: Date.now(), level }
          : sl
      )
    } else {
      targetId = uuid()
      const newEntry: SavedLevel = { id: targetId, name: levelName, savedAt: Date.now(), level }
      newSavedLevels = [...savedLevels, newEntry]
    }

    persistSavedLevels(newSavedLevels)
    set({ savedLevels: newSavedLevels, currentLevelId: targetId, currentLevelName: levelName })
  },

  loadLevel: (id) => {
    const { savedLevels } = get()
    const entry = savedLevels.find((sl) => sl.id === id)
    if (!entry) return

    const { gridSize, arrows } = entry.level
    const geometry = generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z)
    const occupiedEdges = getOccupiedEdges(arrows)
    set({
      gridSize,
      arrows,
      geometry,
      occupiedEdges,
      pendingPath: [],
      selectedArrowId: null,
      removedInTest: [],
      mode: 'add',
      currentLevelId: id,
      currentLevelName: entry.name,
    })
  },

  deleteLevel: (id) => {
    const { savedLevels, currentLevelId } = get()
    const newSavedLevels = savedLevels.filter((sl) => sl.id !== id)
    persistSavedLevels(newSavedLevels)
    set({
      savedLevels: newSavedLevels,
      ...(currentLevelId === id ? { currentLevelId: null } : {}),
    })
  },

  renameLevel: (id, name) => {
    const { savedLevels, currentLevelId, currentLevelName } = get()
    const newSavedLevels = savedLevels.map((sl) =>
      sl.id === id ? { ...sl, name } : sl
    )
    persistSavedLevels(newSavedLevels)
    set({
      savedLevels: newSavedLevels,
      ...(currentLevelId === id ? { currentLevelName: name } : {}),
    })
  },

  newLevel: () => {
    const gridSize: GridSize = { x: 3, y: 3, z: 3 }
    const geometry = generateCubeGeometry(gridSize.x, gridSize.y, gridSize.z)
    set({
      gridSize,
      arrows: [],
      geometry,
      occupiedEdges: new Set(),
      pendingPath: [],
      selectedArrowId: null,
      removedInTest: [],
      mode: 'add',
      currentLevelId: null,
      currentLevelName: 'Untitled Level',
    })
  },

  setCurrentLevelName: (name) => set({ currentLevelName: name }),
}))
