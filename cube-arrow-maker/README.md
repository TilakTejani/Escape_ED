# Cube Arrow — Level Maker

> A browser-based 3D puzzle editor for designing, testing, and exporting levels for the Cube Arrow game.

---

## Table of Contents

1. [Game Concept](#1-game-concept)
2. [Running Locally](#2-running-locally)
3. [Editor Modes](#3-editor-modes)
4. [Building an Arrow (Add Mode)](#4-building-an-arrow-add-mode)
5. [Grid Size](#5-grid-size)
6. [Test Mode](#6-test-mode)
7. [Auto Generate](#7-auto-generate)
8. [Export / Import](#8-export--import)
9. [Blocking Logic (in depth)](#9-blocking-logic-in-depth)
10. [Design Constraints](#10-design-constraints)
11. [Project Structure](#11-project-structure)
12. [API Reference](#12-api-reference)
13. [Tech Stack](#13-tech-stack)

---

## 1. Game Concept

### What Cube Arrow is

Cube Arrow is a 3D sliding-arrow puzzle. The playing field is a rectangular lattice of vertices connected by axis-aligned edges. Each vertex in the grid belongs to exactly one arrow. An arrow is a connected path of vertices through the grid, with one designated end acting as the arrowhead. The goal is to clear every arrow from the grid by tapping them in the correct order.

### The 3D grid

The grid is an **X × Y × Z** lattice — not necessarily cubic. Every pair of vertices that are one grid step apart along any axis is connected by an edge. **Note: The grid is surface-only.** Only the outer faces of the X × Y × Z volume contain clickable vertices; the interior is treated as a solid block and contains no nodes. This ensures all arrows are visible on the exterior at all times.


World coordinates map each axis symmetrically to the range **[-1, 1]**:

```
world_coord(i, n) = i * (2 / (n - 1)) - 1    for i in 0..n-1
```

The first vertex along any axis sits at -1 and the last at +1, with evenly-spaced steps between them.

### What an arrow is

An arrow is an ordered sequence of vertex indices (`path: number[]`) where:

- Every consecutive pair of vertices in the path is connected by a grid edge.
- No vertex appears twice.
- No edge appears twice.
- The path has at least 2 vertices (at least 1 edge / segment).

One end of the path is designated the **arrowhead** (`headEnd: 'start' | 'end'`). The head is rendered as a cone; the rest of the path is rendered as a line. The direction the cone points is the direction the arrow will travel when tapped.

### How tapping works

When a player taps an arrow, it slides out of the grid in the direction its arrowhead is pointing. The arrow continues in a straight line — one grid step at a time — until it leaves the bounds of the grid entirely. For the tap to succeed, every vertex and every edge along that exit path must be unoccupied by any other arrow still on the board. If anything blocks the exit path, the tap has no effect.

### Blocking logic

An arrow is blocked if, starting from its head vertex and stepping one grid unit at a time in the head direction, any of the following is met before leaving the grid:

- A vertex along the exit path is part of another arrow's body.
- An edge along the exit path (the connection between two consecutive steps) is occupied by another arrow.

Both vertex and edge occupancy checks must pass all the way to the grid boundary for the tap to succeed.

### The goal

Clear all arrows from the grid. An arrow is cleared when it successfully exits. Because the exit path of one arrow may run through vertices and edges used by others, the order in which arrows are tapped matters — some can only be tapped after others have already been removed.

---

## 2. Running Locally

**Prerequisites:** Node.js 18+ and npm 9+.

```bash
# Install dependencies
npm install

# Start the development server
npm run dev
```

The app is available at **http://localhost:3000**.

Other available scripts:

```bash
npm run build   # Production build
npm run start   # Serve the production build
npm run lint    # Run ESLint
```

---

## 3. Editor Modes

The left panel's **Mode** section lets you switch between three mutually exclusive modes. Switching mode resets the pending path and clears any active selection.

| Mode | Label | What it does |
|------|-------|--------------|
| `add` | Add Arrow | Click vertex dots or edges on the 3D canvas to build a path. Confirm or cancel using the panel controls. |
| `select` | Select | Click any placed arrow to select it. The panel shows the path indices, head position, edge count, and a Delete button. |
| `test` | Test | Simulate gameplay. Click arrows to tap them. The panel shows a progress bar, blocked-arrow indicators, Auto Solve, and Reset Test. |

Switching to Test mode resets `removedInTest` to empty so every simulation starts fresh.

---

## 4. Building an Arrow (Add Mode)

### Step-by-step

1. Switch to **Add Arrow** mode.
2. Click any unoccupied vertex dot on the 3D canvas. This becomes the first vertex of the pending path. The dot turns blue and gains a selection ring.
3. Continue clicking adjacent unoccupied vertices or the edge lines between them to extend the path. Reachable next vertices are highlighted in green with a pulse ring.
4. In the left panel, choose whether the arrowhead sits at the **Start** (`← Start`) or **End** (`End →`) of the path.
5. Click **Add Arrow** (requires at least 2 vertices). If a design constraint is violated, an error message is shown inline and the arrow is not committed.
6. To discard the work in progress, click the **✕** cancel button.

### Validation rules

Rules 1–4 are enforced incrementally as you extend the path (invalid extensions are silently rejected). Rules 5–6 are enforced only when you click **Add Arrow**.

| Rule | What is checked | When enforced | Error message |
|------|-----------------|---------------|---------------|
| Adjacency | Each new vertex must share a grid edge with the previous vertex in the path. | Incremental | Silent rejection |
| Occupied edge | The edge between the current path tail and the proposed vertex must not already belong to a committed arrow. | Incremental | Silent rejection |
| Self-loop prevention | A vertex already in the pending path cannot be added again. | Incremental | Silent rejection |
| No directly facing arrowheads | The new arrow's head must not lie on the exit path of any existing arrow whose head also lies on the new arrow's exit path — no two heads may point directly at each other. | On confirm | "Two arrowheads cannot directly face each other." |
| No self-pointing arrow | The new arrow's exit path must not re-enter any vertex of the arrow's own body. | On confirm | "Arrow cannot point back at its own path." |

---

## 5. Grid Size

### Axes and constraints

Each axis (X, Y, Z) is configured independently. The minimum value for any axis is **2** (the smallest grid that can have an edge) and the maximum is **10**. The axes do not need to match — asymmetric grids like 2 × 5 × 3 are fully supported.

### The +/- steppers

The bottom bar contains a stepper (`−` / `+`) for each axis. Changing any axis value prompts for confirmation if there are placed arrows, because changing the grid size clears all arrows (the vertex indices in existing paths would be invalidated). The live vertex count and edge count are shown next to the steppers.

### Vertex indexing formula

Vertices are stored in a flat array ordered Z-outer, Y-middle, X-inner:

```
idx(x, y, z) = z × (nx × ny) + y × nx + x
```

Where `nx`, `ny`, `nz` are the number of vertices along each axis.

### World coordinates

Each axis maps grid indices to world space symmetrically:

```
step_x = 2 / (nx - 1)
step_y = 2 / (ny - 1)
step_z = 2 / (nz - 1)

world_x(x) = x * step_x - 1
world_y(y) = y * step_y - 1
world_z(z) = z * step_z - 1
```

### Example: 3 × 3 × 3 grid

For a 3 × 3 × 3 grid, `step = 1.0` on all axes. The 27 vertices are:

| Grid (x, y, z) | Index | World (x, y, z) |
|----------------|-------|-----------------|
| (0, 0, 0) | 0 | (-1, -1, -1) |
| (1, 0, 0) | 1 | (0, -1, -1) |
| (2, 0, 0) | 2 | (1, -1, -1) |
| (0, 1, 0) | 3 | (-1, 0, -1) |
| (1, 1, 0) | 4 | (0, 0, -1) |
| (2, 1, 0) | 5 | (1, 0, -1) |
| (0, 2, 0) | 6 | (-1, 1, -1) |
| (1, 2, 0) | 7 | (0, 1, -1) |
| (2, 2, 0) | 8 | (1, 1, -1) |
| (0, 0, 1) | 9 | (-1, -1, 0) |
| (1, 1, 1) | 13 | (0, 0, 0) — centre |
| (2, 2, 2) | 26 | (1, 1, 1) |

The centre vertex is index 13 (x=1, y=1, z=1) at world position (0, 0, 0).

---

## 6. Test Mode

### How tapping works at runtime

Switching to Test mode resets `removedInTest` to empty without altering placed arrows. In test mode, clicking an arrow calls `tapArrow(id)`, which:

1. Computes the set of arrows still present on the board: all arrows whose IDs are not in `removedInTest`.
2. Calls `canArrowExit` with the tapped arrow's ID against that remaining set.
3. If the arrow can exit, its ID is appended to `removedInTest` and it disappears from the canvas.
4. If it is blocked, nothing happens.

### Red arrowheads — blocked arrows

When `hideBlocked` is off (the default), any arrow for which `canArrowExit` currently returns false has its cone rendered in **red** (`#ef4444`). This gives a real-time visual hint about which arrows are free to tap and which are waiting for others to be removed first.

### "Hide blocked" toggle

The **Hide blocked** button in the test panel toggles the `hideBlocked` state. When on:

- All arrow cones render in the default dark colour (`#1e293b`), regardless of exit status.
- The "Red arrows are blocked. Tap in the correct order to clear." hint text is hidden from the panel.

This creates a harder play-test experience where the player must reason about the tap order without colour hints.

### Auto Solve

The **Auto Solve** button runs the solver automatically at 50 ms intervals. On each tick it calls `tapFirstRemovable`:

- `tapFirstRemovable` iterates over the remaining arrows and taps the first one that `canArrowExit` returns true for.
- It returns `true` if a tap was performed, or `false` if no arrow can currently exit.
- When it returns `false`, the auto-solver stops. This happens either because all arrows have been cleared (puzzle solved) or because the puzzle has reached a deadlock — no remaining arrow can exit. Deadlocks should not occur for auto-generated levels but may occur for manually constructed levels that violate the solvability invariant.

The **Stop** button interrupts the timer at any point. Clicking **Reset Test** while auto-solving also stops it.

### Progress bar

The test panel shows `removedInTest.length / arrows.length` as a percentage, rendered as a violet-filled bar on a grey track. When all arrows are cleared, the progress bar is replaced with a "Level Solved!" confirmation banner in emerald green.

### Reset Test

**Reset Test** sets `removedInTest` back to `[]`, restoring all arrows to the board and resetting the progress bar to zero. It also stops any running Auto Solve timer.

---

## 7. Auto Generate

Auto Generate creates a complete, solvable level that covers every vertex in the grid with exactly one arrow. It is triggered by clicking **Generate Level** in the left panel. After generation the mode automatically switches to Test so you can play-test immediately.

### Parameter: Difficulty

The **Difficulty** setting (Easy, Medium, Hard) affects the maximum length of the generated arrows. Harder levels feature longer, more winding paths that create complex interlocking dependencies.


### Full algorithm

`autoGenerateLevel(gridSize, maxPathLength)` runs in up to three passes, returning the first result that satisfies the required constraints.

---

#### Phase 1: Backtracking Partition

The algorithm covers **100% of the grid** with no empty vertices. It uses a recursive DFS to partition the surface into paths of valid length. If it reaches a state where a vertex is stranded (no neighbors to form a path), it backtracks and reroutes previous paths.

#### Phase 2: Backtracking Reverse Construction

Arrows are assigned head-ends using a recursive **Solvable by Construction** search.

1. Pick an unassigned path.
2. Assign a head-end (`start` or `end`).
3. Verify if that arrow can exit given the arrows already placed in this pass.
4. If yes, recurse to the next path.
5. If both orientations are blocked, backtrack to the previous path and try its other orientation.

This guarantees that every generated level is solvable. The removal order is the exact reverse of the assignment order.


---

#### Phase 3: Design Validity

Even a solvable puzzle can be unpleasant if it contains configurations that look confusing or unsolvable at a glance. After Phase 2, `isDesignValid` runs two checks:

| Check | Function | What it detects |
|-------|----------|-----------------|
| Self-pointing | `arrowPointsAtItself` | Any arrow whose exit path re-enters its own body — the arrow would appear to "chase itself". |
| Directly facing | `arrowsDirectlyFacing` | Any pair of arrows whose heads point directly at each other — looks like a mutual deadlock even though one can be tapped first. |

#### Pass Strategy

The generator uses a single, robust backtracking search that finds a valid configuration without needing multiple "pass" retries.


---

## 8. Export / Import

### Exporting

The **Export JSON** button in the bottom bar is enabled when at least one arrow is placed. Clicking it serialises the current level and triggers a browser download named:

```
cube-arrow-level-{timestamp}.json
```

### Importing

The **Import** button opens a file picker filtered to `.json` files. The selected file is parsed and loaded via `importLevel`, which replaces the current grid size and all arrows, and resets all test state. If the file cannot be parsed as JSON, an alert is shown.

### JSON format

```json
{
  "gridSize": {
    "x": 3,
    "y": 3,
    "z": 3
  },
  "arrows": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "path": [0, 1, 4],
      "headEnd": "end"
    },
    {
      "id": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
      "path": [13, 12, 9],
      "headEnd": "start"
    }
  ]
}
```

### Field reference

| Field | Type | Description |
|-------|------|-------------|
| `gridSize.x` | `number` | Number of vertices along the X axis (2–10). |
| `gridSize.y` | `number` | Number of vertices along the Y axis (2–10). |
| `gridSize.z` | `number` | Number of vertices along the Z axis (2–10). |
| `arrows[].id` | `string` | UUID v4. Unique identifier for this arrow. |
| `arrows[].path` | `number[]` | Ordered vertex indices. Minimum length 2. Each consecutive pair must share a grid edge. |
| `arrows[].headEnd` | `'start' \| 'end'` | Which end of `path` is the arrowhead. `'end'` means `path[path.length - 1]` is the head; `'start'` means `path[0]` is the head. |

There is no `color` field — all arrows use the same visual style determined by the renderer at runtime.

### Reconstructing world positions from a loaded level

```
idx(x, y, z) = z * (gridSize.x * gridSize.y) + y * gridSize.x + x
world_x = x * (2 / (gridSize.x - 1)) - 1
world_y = y * (2 / (gridSize.y - 1)) - 1
world_z = z * (2 / (gridSize.z - 1)) - 1
```

---

## 9. Blocking Logic (in depth)

### `getExitDirection`

```ts
getExitDirection(vertices, path, headEnd): [number, number, number]
```

Returns the **normalised** unit vector pointing in the arrow's exit direction. It identifies the two vertices at the head end of the path:

- `headEnd === 'end'`: the pair is `[path[n-2], path[n-1]]` (penultimate → last).
- `headEnd === 'start'`: the pair is `[path[1], path[0]]` (second → first).

The raw difference `(dx, dy, dz)` is divided by its magnitude to produce a unit vector. This is used only for rendering the cone's orientation, not for the exit-path walk.

### `canArrowExit` — step-by-step walk

```ts
canArrowExit(arrowId, arrows, geometry, gridSize): boolean
```

This function does **not** use the normalised direction from `getExitDirection`. Instead it derives the **actual grid step vector** directly from the last edge at the head end:

```ts
const dx = hx - tx1   // raw world-space difference, not normalised
const dy = hy - ty1
const dz = hz - tz1
```

This preserves the true grid spacing. On a 3 × 3 × 3 grid the step magnitude is 1.0; on a 2 × 2 × 2 grid it is 2.0; on a 4 × 4 × 4 grid it is 2/3. The walk then proceeds:

1. Start at `currentVertex = headV`.
2. Compute the candidate next world position: `(cx + dx, cy + dy, cz + dz)`.
3. Search the vertex array for a vertex within 0.0001 world units of that position.
4. If no vertex is found, the arrow has left the grid — return `true`.
5. If a vertex is found (`nextVertex`), check every other arrow:
   - **Vertex check**: if `nextVertex` appears anywhere in another arrow's `path`, return `false`.
   - **Edge check**: if the edge `(currentVertex, nextVertex)` appears as consecutive entries (in either direction) in another arrow's `path`, return `false`.
6. Set `currentVertex = nextVertex` and repeat from step 2.

### Why normalised direction would fail for non-cubic grids

Consider a 5 × 2 × 2 grid. The X step in world space is `2 / (5-1) = 0.5`, while Y and Z steps are `2 / (2-1) = 2.0`. An arrow pointing along X has a raw step vector of `(0.5, 0, 0)`. Its normalised direction is `(1, 0, 0)`. If the walker advanced by the normalised step, it would look for a vertex at `head_x + 1.0` — but adjacent vertices along X are only 0.5 apart. The search would find nothing and incorrectly conclude the arrow has already left the grid. Using the raw step vector avoids this entirely, making the logic correct for all grid dimensions.

---

## 10. Design Constraints

### `arrowsDirectlyFacing`

```ts
arrowsDirectlyFacing(a1, a2, vertices): boolean
```

Two arrows are "directly facing" when their heads point straight at each other across open space. Formally:

- Walk the exit path of `a1` (the vertices strictly beyond its head, computed with the same step-by-step walk as `canArrowExit`) and collect those vertex indices.
- Walk the exit path of `a2` similarly.
- The arrows are directly facing if `a2`'s head vertex appears in `a1`'s exit-path vertices **and** `a1`'s head vertex appears in `a2`'s exit-path vertices.

Both conditions must be true simultaneously. It is not sufficient for one head to be aimed at the other's body; both heads must lie on each other's exit ray. This constraint is checked at `confirmArrow` time in the editor and in Auto Generate Pass 1.

### `arrowPointsAtItself`

```ts
arrowPointsAtItself(arrow, vertices): boolean
```

Uses the internal `exitPathVertices` helper — which walks the exit ray and returns vertex indices beyond the head — to collect all vertices the arrow would pass through when exiting.

If any of those exit-path vertices is a member of the arrow's own `path` set, the function returns `true`. This indicates the arrow's exit trajectory loops back into its own body — a configuration that arises in non-straight paths where the head end is oriented toward the body. This constraint is checked at `confirmArrow` time and in Auto Generate Pass 1.

---

## 11. Project Structure

```
cube-arrow-maker/
├── app/
│   ├── layout.tsx          # Root HTML shell; sets page title and global overflow:hidden
│   ├── page.tsx            # Single route — renders <LevelMaker />
│   └── globals.css         # Tailwind base import and global styles
│
├── components/
│   ├── LevelMaker.tsx      # Top-level layout: LeftPanel + Canvas3D + BottomBar
│   ├── Canvas3D.tsx        # React Three Fiber <Canvas> wrapper (SSR-disabled via next/dynamic)
│   ├── CubeScene.tsx       # All 3D objects: vertices, edges, arrows, lighting, orbit controls
│   ├── LeftPanel.tsx       # Sidebar: mode switcher, add/select/test panels, auto generate
│   └── BottomBar.tsx       # Footer: grid-size steppers, clear/import/export controls
│
├── lib/
│   ├── cube.ts             # Pure geometry and puzzle logic (no React, no store dependencies)
│   └── generator.ts        # autoGenerateLevel — vertex partition + reverse construction
│
├── store/
│   └── levelStore.ts       # Zustand store: all editor state and every action
│
├── types/
│   └── index.ts            # TypeScript interfaces: Arrow, GridSize, Level, EditorMode, CubeGeometry
│
├── package.json
└── tsconfig.json
```

---

## 12. API Reference

### `lib/cube.ts`

All functions are pure — no side effects, no store access, no React dependencies.

---

#### `generateCubeGeometry`

```ts
function generateCubeGeometry(nx: number, ny: number, nz: number): CubeGeometry
```

Generates the full vertex and edge lists for an `nx × ny × nz` grid.

- **Vertices**: all `nx * ny * nz` vertices in Z-outer, Y-middle, X-inner order. World coordinates mapped to `[-1, 1]` per axis.
- **Edges**: all axis-aligned edges — X-direction edges where `x < nx-1`, Y-direction edges where `y < ny-1`, Z-direction edges where `z < nz-1`.
- Works for any `nx, ny, nz ≥ 2` without special-casing.

Returns `{ vertices: [number, number, number][], edges: [number, number][] }`.

---

#### `areAdjacent`

```ts
function areAdjacent(edges: [number, number][], v1: number, v2: number): boolean
```

Returns `true` if there is a grid edge between `v1` and `v2` in either direction. Used by `addVertexToPending` to enforce the adjacency rule when extending a pending path.

---

#### `edgeKey`

```ts
function edgeKey(v1: number, v2: number): string
```

Returns a canonical string key for an undirected edge: `"${Math.min(v1,v2)}-${Math.max(v1,v2)}"`. Used to build `Set<string>` collections of occupied edges for O(1) occupancy lookups.

---

#### `getExitDirection`

```ts
function getExitDirection(
  vertices: [number, number, number][],
  path: number[],
  headEnd: 'start' | 'end'
): [number, number, number]
```

Returns the **normalised** unit vector of the arrow's head direction. Used by `CubeScene` to orient the arrowhead cone mesh. Returns `[0, 0, 0]` for paths shorter than 2 vertices.

---

#### `canArrowExit`

```ts
function canArrowExit(
  arrowId: string,
  arrows: Arrow[],
  vertices: [number, number, number][],
  edges: [number, number][]
): boolean
```

The core puzzle-logic function. Determines whether the arrow identified by `arrowId` can slide out of the grid given the current `arrows` array. Uses the raw (non-normalised) grid step vector for the exit walk to correctly handle non-cubic grids. See [Section 9](#9-blocking-logic-in-depth) for a complete explanation.

---

#### `getOccupiedEdges`

```ts
function getOccupiedEdges(arrows: Arrow[]): Set<string>
```

Returns a `Set` of `edgeKey` strings for every edge used by any arrow in the input array. Used by the add-mode UI to prevent placing new arrows on already-occupied edges.

---

#### `arrowPointsAtItself`

```ts
function arrowPointsAtItself(arrow: Arrow, vertices: [number, number, number][]): boolean
```

Returns `true` if the arrow's exit path re-enters any vertex of its own body. See [Section 10](#10-design-constraints).

---

#### `arrowsDirectlyFacing`

```ts
function arrowsDirectlyFacing(
  a1: Arrow,
  a2: Arrow,
  vertices: [number, number, number][]
): boolean
```

Returns `true` if the two arrows' heads are aimed directly at each other. See [Section 10](#10-design-constraints).

---

#### `getNeighbors`

```ts
function getNeighbors(edges: [number, number][], vertex: number): number[]
```

Returns all vertex indices directly connected to `vertex` by a single edge. Used by the generator's greedy walk and the `pickMostConstrained` heuristic.

---

### `lib/generator.ts`

#### `autoGenerateLevel`

```ts
function autoGenerateLevel(gridSize: GridSize, _: number, difficulty: Difficulty): Arrow[]
```

Generates a complete, solvable set of arrows that covers every surface vertex in the grid exactly once.

- `gridSize`: the X/Y/Z dimensions of the grid.
- `difficulty`: Easy, Medium, or Hard. Affects path length and interdependence.

Returns an `Arrow[]` in randomised order. The returned set is guaranteed to be solvable and cover the entire grid.


---

### `store/levelStore.ts`

#### State fields

| Field | Type | Initial value | Description |
|-------|------|---------------|-------------|
| `gridSize` | `GridSize` | `{ x: 3, y: 3, z: 3 }` | Current grid dimensions. |
| `arrows` | `Arrow[]` | `[]` | All committed, placed arrows. |
| `selectedArrowId` | `string \| null` | `null` | ID of the currently selected arrow in Select mode. |
| `mode` | `EditorMode` | `'add'` | Active editor mode: `'add'`, `'select'`, or `'test'`. |
| `pendingPath` | `number[]` | `[]` | Vertex indices of the arrow being built in Add mode. |
| `pendingHeadEnd` | `'start' \| 'end'` | `'end'` | Head-end selection for the pending arrow. |
| `removedInTest` | `string[]` | `[]` | IDs of arrows that have been successfully tapped in Test mode. |
| `hideBlocked` | `boolean` | `false` | When `true`, all cones render in the default dark colour; the red-blocked hint is suppressed. |
| `pendingError` | `string \| null` | `null` | Error message from a failed `confirmArrow`, displayed inline in the left panel. |

#### Actions

| Action | Signature | Description |
|--------|-----------|-------------|
| `setGridSize` | `(g: GridSize) => void` | Updates grid dimensions and clears all arrows, pending path, and selection. |
| `setMode` | `(mode: EditorMode) => void` | Switches editor mode. Resets pending path, selection, and `hideBlocked`. Resets `removedInTest` when switching to `'test'`. |
| `toggleHideBlocked` | `() => void` | Flips `hideBlocked` between `true` and `false`. |
| `addVertexToPending` | `(vertexIndex: number) => void` | Appends a vertex to the pending path after validating adjacency, edge occupancy, and self-loop rules. Silently ignores invalid additions. |
| `setPendingHeadEnd` | `(end: 'start' \| 'end') => void` | Sets which end of the pending path will be the arrowhead. |
| `confirmArrow` | `() => void` | Commits the pending path as a new arrow after validating design constraints. Sets `pendingError` and aborts if `arrowPointsAtItself` or `arrowsDirectlyFacing` fails. Requires `pendingPath.length >= 2`. |
| `cancelPending` | `() => void` | Discards the pending path and clears any pending error. |
| `clearPendingError` | `() => void` | Clears `pendingError` without affecting the pending path. |
| `selectArrow` | `(id: string \| null) => void` | Sets `selectedArrowId`. Pass `null` to deselect. |
| `deleteArrow` | `(id: string) => void` | Removes an arrow by ID. Clears `selectedArrowId` if the deleted arrow was selected. |
| `tapArrow` | `(id: string) => void` | Test mode: adds the arrow's ID to `removedInTest` if `canArrowExit` returns true for the current remaining set. No-ops if blocked. |
| `tapFirstRemovable` | `() => boolean` | Finds the first remaining arrow that can currently exit and taps it. Returns `true` if a tap was performed, `false` if no arrow can exit. Used by Auto Solve. |
| `resetTest` | `() => void` | Clears `removedInTest`, restoring all arrows to the board. |
| `generateArrows` | `(maxPathLen: number) => void` | Calls `autoGenerateLevel`, replaces all arrows, resets test state, and switches mode to `'test'`. |
| `clearAll` | `() => void` | Removes all arrows and resets selection and pending path. |
| `importLevel` | `(level: Level) => void` | Replaces grid size and all arrows with data from a parsed level object. Resets all transient state. |
| `exportLevel` | `() => Level` | Returns a `Level` snapshot (`gridSize` + `arrows`) suitable for JSON serialisation. |

---

### Components

| Component | File | Description |
|-----------|------|-------------|
| `LevelMaker` | `components/LevelMaker.tsx` | Root layout. Composes `LeftPanel`, `Canvas3D`, and `BottomBar` into a full-screen flex layout. Loads `Canvas3D` via `next/dynamic` with `ssr: false` to prevent Three.js from running during server-side rendering. |
| `Canvas3D` | `components/Canvas3D.tsx` | Thin wrapper around React Three Fiber's `<Canvas>`. Sets the initial camera position at `(3, 2.5, 3)` with a 50° FOV, enables antialiasing and shadows, and renders `<CubeScene>` inside a `<Suspense>` boundary. |
| `CubeScene` | `components/CubeScene.tsx` | All 3D content: ambient and directional lights, `<OrbitControls>` (pan disabled, zoom enabled, damping 0.08), vertex dots (`VertexDot`), pending-path edge lines (`EdgeLine`), and placed arrows (`ArrowMesh`). Computes the set of reachable-next vertices for the add-mode highlight. All interactive meshes use an invisible oversized hit-box and a click-vs-drag guard (8 px movement threshold) to prevent orbit drags from registering as vertex clicks. |
| `LeftPanel` | `components/LeftPanel.tsx` | The 256 px sidebar. Contains the mode switcher, Add Arrow panel (path status, head-end toggle, confirm/cancel, inline error display), Select panel (path indices, edge count, delete), Test panel (progress bar, hide-blocked toggle, auto solve with 50 ms timer, reset), Auto Generate section (max-length stepper, generate button with loading spinner), and a scrollable arrow list at the bottom. |
| `BottomBar` | `components/BottomBar.tsx` | The 56 px footer. Contains the X/Y/Z axis steppers with live vertex and edge count, a Clear button, an Import button (triggers a hidden `<input type="file">` element), and a disabled-when-empty Export JSON button. |

---

## 13. Tech Stack

| Dependency | Version | Role |
|-----------|---------|------|
| Next.js | 16.2.1 | React framework; App Router; `next/dynamic` for SSR-safe Three.js loading |
| React | 19.2.4 | UI library |
| React DOM | 19.2.4 | DOM renderer |
| Three.js | ^0.183.2 | 3D rendering engine |
| `@react-three/fiber` | ^9.5.0 | React renderer for Three.js scenes |
| `@react-three/drei` | ^10.7.7 | Three.js helpers: `<OrbitControls>`, `<Line>` |
| Zustand | ^5.0.12 | Lightweight global state management |
| uuid | ^13.0.0 | UUID v4 generation for arrow IDs |
| Tailwind CSS | ^4 | Utility-first CSS; PostCSS integration via `@tailwindcss/postcss` |
| TypeScript | ^5 | Static typing throughout the codebase |
