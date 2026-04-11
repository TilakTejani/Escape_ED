# Cube Arrow Maker — Logic Documentation

## Overview

This tool generates and edits levels for Escape-ƎD: a puzzle game where arrows are placed on the surface of a 3D cube grid. The player must tap arrows in the correct order to slide them off the cube. An arrow can only exit if its path ahead (in the direction it points) is clear.

There are four layers of logic:
1. **Geometry** — how the cube surface is modelled as a graph
2. **Arrow model** — what an arrow is
3. **Validation** — what makes a valid arrow placement and a solvable level
4. **Generator** — how levels are auto-generated

---

## 1. Cube Geometry (`lib/cube.ts`)

### Surface-only vertex model

The cube is an `nx × ny × nz` grid. Instead of working with the 3D cube mesh, the system models only the **surface** of the grid. Each surface cell (tile) is treated as a **pseudo-vertex** — a node in a graph.

A position `(x, y, z)` is a surface tile if at least one coordinate is at its minimum (0) or maximum (n-1):

```
isSurface(x, y, z) = x==0 || x==nx-1 || y==0 || y==ny-1 || z==0 || z==nz-1
```

For a 3×3×3 grid: 27 total cells − 1 interior = **26 surface vertices**.

### Two coordinate systems

Each vertex carries two representations:

| Field | Type | Purpose |
|---|---|---|
| `vertices[i]` | `[float, float, float]` | Centred float position for rendering. Origin at cube centre. |
| `gridCoords[i]` | `[int, int, int]` | Integer grid position `(x, y, z)` for directional logic. |

They describe the same tile, just in different spaces. `vertices[i] = gridCoords[i] - center`.

`posMap` is a `Map<"x,y,z", index>` for O(1) lookup from integer coords back to vertex index — used by the exit-walking algorithm.

### Graph edges

Two surface vertices are connected by an edge if they are adjacent in the integer grid (differ by exactly 1 in exactly one axis, and both are surface tiles).

Edges are generated in O(V) by checking only `+x`, `+y`, `+z` neighbours per vertex:

```
for each surface vertex (x,y,z):
    if (x+1, y, z) is also surface → add edge
    if (x, y+1, z) is also surface → add edge
    if (x, y, z+1) is also surface → add edge
```

`adjSet` stores all edge keys (`"min-max"`) for O(1) adjacency queries.

### What edges mean physically

On a flat face, edges connect horizontally/vertically adjacent tiles — straightforward.  
At a geometric edge of the cube, two tiles on different faces are adjacent. This allows arrows to **wrap around corners** of the cube, which is the core mechanic.

---

## 2. Arrow Model (`types/index.ts`)

```typescript
interface Arrow {
  id: string
  path: number[]                    // ordered sequence of vertex indices
  headEnd: 'start' | 'end'         // which end of the path the arrowhead is at
  headDir: [number, number, number] // integer delta pointing in exit direction
}
```

An arrow is a **path** through the surface graph (no repeated vertices, adjacent vertices only). One end is the **head** (where the arrowhead is) and the other is the **tail**.

`headDir` is the integer delta from the second-to-last vertex to the last vertex at the head end. Example: head at `(2,1,0)`, tail-adjacent at `(1,1,0)` → `headDir = [1,0,0]`. This is stored redundantly for fast rendering without recomputing from geometry.

---

## 3. Validation (`lib/cube.ts`)

### Can an arrow exit?

`canArrowExit(arrowId, arrows, geometry, gridSize)` answers: **can this arrow currently slide off the cube?**

Algorithm — integer delta-walk from the head:
1. Compute `headDir = gridCoords[head] - gridCoords[tail-adjacent]`
2. Starting at `head`, step repeatedly by `headDir`
3. At each step:
   - If the new position is outside the grid bounds → **can exit** ✓
   - If the new position has no surface vertex (solid core of the cube) → **blocked** ✗
   - If the new position is occupied by any other arrow's vertex → **blocked** ✗
   - If the new position crosses an occupied edge → **blocked** ✗

The "solid core" check is what catches arrows pointing into the interior — the exit ray hits a non-surface cell.

### Placement constraints

When manually placing an arrow:

- **No self-pointing**: `arrowPointsAtItself` — the exit ray from the newly placed arrow must not hit any of its own vertices.
- **No head-on collision**: `arrowsDirectlyFacing` — two arrowheads cannot point directly at each other (each would hit the other's head on its exit ray, meaning neither could ever exit).

### Occupied tracking

The store caches two sets for O(1) placement validation:

- `occupiedEdges: Set<string>` — all edges used by placed arrows. Prevents new arrows from sharing an edge.
- `occupiedVertices: Set<number>` — all vertices used by placed arrows. Prevents new arrows from sharing a vertex.

Both are incrementally updated on every mutation (add arrow, delete arrow, generate, import, etc.).

---

## 4. Auto-Generator (`lib/generator.ts`)

The generator produces a complete valid level automatically. It runs in two phases:

### Phase 1: `buildPaths` — construct arrow paths

**Goal**: partition as many surface vertices as possible into valid paths (length ≥ 2), with no vertex shared between paths.

**Algorithm**: Greedy Warnsdorff-style traversal with isolated-vertex rescue.

#### Step 1 — Choose a start vertex

Pick the unvisited vertex with lowest `freeDeg` (number of unvisited neighbours). Vertices with `freeDeg ≤ 1` are prioritised strongly (they're about to become unreachable). Others get heavy random noise so arrows don't always start at the same corner.

#### Step 2 — Extend the path

At each step, score every unvisited neighbour and pick the best:

```
score(nb) = freeDeg[nb]                          // Warnsdorff: prefer constrained next
           + newFaces(nb) * facePenalty * 0.5    // difficulty: penalise crossing faces
           + random() * 3.0                      // noise: avoid determinism
           ± straightness bias                   // straightness param
```

**Straightness bias**: if the candidate `nb` continues in the same direction as the last step, subtract `(straightness - 0.5) * 6`. With `straightness = 1.0`, straight steps score much lower (preferred); with `straightness = 0.0`, turns are preferred.

**Face penalty**: `faceMasks[v]` is a 6-bit bitmask where each bit represents one of the 6 cube faces that vertex `v` touches. `newFaces = popcount(faceMask[nb] & ~currentPathMask)` counts how many new faces `nb` would introduce. A high `facePenalty` (easy difficulty) discourages crossing between faces — arrows stay on one face. `facePenalty = 0` (hard) means no penalty, arrows freely wrap around the cube.

#### Step 3 — Rescue isolated vertices

After visiting any vertex `v`, check all its neighbours. If any neighbour `u` now has `freeDeg = 0` (all its neighbours are visited, and it hasn't been visited itself), `u` would be permanently stranded. **Rescue it immediately**:

1. **Tail extension**: if `u` is adjacent to the tail of a committed path, extend that path with `u`
2. **Head reversal**: if `u` is adjacent to the head of a committed path, reverse that path then extend with `u`
3. **Middle split**: if `u` is adjacent to a middle vertex of a committed path, split the path at that vertex — left half gets `u` appended, right half becomes a new path

This guarantees near-100% coverage without backtracking.

#### Difficulty config

| Difficulty | `facePenalty` | `preferredMaxTraj` | `preferLong` |
|---|---|---|---|
| easy | 8 | 1 | false |
| medium | 2 | 3 | false |
| hard | 0 | 999 | true |

- `facePenalty = 8`: arrows heavily prefer staying on one face → easy to see their direction
- `facePenalty = 0`: arrows freely cross faces → harder to predict exit direction
- `preferredMaxTraj`: how many tiles ahead the exit trajectory should ideally be (shorter = easier to see which arrows are removable)
- `preferLong`: hard mode prefers long exit trajectories (arrow blocked far away, harder to assess)

---

### Phase 2: `assignExits` — assign head ends

**Goal**: for each path, decide which end is the arrowhead, such that:
- The arrowhead's exit ray doesn't hit the solid core (`exitsCleanly`)
- The exit ray doesn't loop back onto the arrow itself (`selfBlocked`)
- The exit ray of the final arrangement doesn't conflict with other arrows' bodies

**Algorithm**:

1. Precompute `ExitInfo` for both ends (`'start'` and `'end'`) of every path:
   - `exitsCleanly`: exit ray leaves the grid bounds without hitting solid core
   - `selfBlocked`: exit ray re-enters the arrow's own vertices
   - `trajectory`: list of surface vertices the exit ray passes through

2. Sort paths by "minimum valid trajectory length" (shorter trajectories first for non-hard, longer first for hard). This ensures the most constrained paths are assigned first.

3. For each path (in sorted order):
   - Prefer ends where `trajectory.length ≤ preferredMaxTraj` ("preferred" exits)
   - Fall back to any valid exit
   - Check that the chosen exit trajectory doesn't pass through any already-placed arrow's vertex or edge
   - If no valid assignment exists → return `null` (trigger a retry)

4. If all 20 attempts fail → use `fallbackAssign` which ignores cross-arrow blocking (guarantees a level, though not perfectly valid).

---

### Retry loop

`autoGenerateLevel` runs `buildPaths → assignExits` up to 20 times. Each attempt uses different random choices in `chooseStart` and `scoreStep`. This handles edge cases where the first path layout makes exit assignment impossible.

---

## 5. Store (`store/levelStore.ts`)

The Zustand store is the single source of truth for the editor state.

### State

| Field | Purpose |
|---|---|
| `gridSize` | Dimensions of the cube grid |
| `arrows` | All placed arrows |
| `geometry` | Cached `CubeGeometry` — recomputed only when `gridSize` changes |
| `occupiedEdges` | Set of all edge keys used by arrows |
| `occupiedVertices` | Set of all vertex indices used by arrows |
| `pendingPath` | Vertices clicked so far in the current arrow being drawn |
| `pendingHeadEnd` | Which end of the pending path will be the head |
| `mode` | `'add'` / `'select'` / `'test'` |
| `removedInTest` | Arrow IDs already tapped off in test mode |

### Key flows

**Drawing an arrow (add mode)**:
1. Click vertex → `addVertexToPending`: checks not occupied, not already pending, adjacent to last, edge not occupied
2. Click confirm → `confirmArrow`: validates no self-pointing, no head-on collision, updates both occupied sets

**Test mode**:
- `tapArrow(id)`: runs `canArrowExit` on the current remaining arrows. If it can exit, adds to `removedInTest`.
- `tapFirstRemovable()`: used by the "hint" button — finds and removes the first arrow that can currently exit.

**Generation**:
- `generateArrows(maxPathLen, difficulty)` calls `autoGenerateLevel`, then auto-names the level `"3x3x3_hard_40%"` (grid size + difficulty + turn percentage).

**Persistence**:
- Levels are saved to `localStorage` under key `'escape-ed-levels'` as `SavedLevel[]` (JSON).
- `saveCurrentLevel` upserts by `currentLevelId`. New levels get a UUID.

---

## 6. Rendering (`components/CubeScene.tsx`)

Rendered with React Three Fiber (R3F).

- **`CubeFaces`**: 6 flat planes, one per cube face, at the correct depth for the surface vertices
- **`EdgeLine`**: one line per graph edge, with a fat invisible hit box for easy clicking. Clicking an edge mid-path auto-selects the unvisited endpoint.
- **`VertexDot`**: one sphere per vertex. Colour-coded: grey (free), green (reachable from current path end), purple (in pending path), cyan (last pending vertex).
- **`ArrowMesh`**: line + arrowhead cone per placed arrow. In test mode, red cone = blocked, can't exit yet.

### Corner routing

`getRenderPoints` inserts a corner waypoint when an arrow segment crosses from one cube face to another. Without this, the line would cut through the cube interior. The waypoint is placed at the geometric edge (the shared corner of the two faces), making the line hug the cube surface.

---

## Data flow summary

```
User clicks vertex
  → addVertexToPending (validates adjacency, occupancy)
  → pendingPath grows

User confirms
  → confirmArrow (validates self-point, head-on)
  → arrow added, occupiedEdges + occupiedVertices updated

User clicks Generate
  → autoGenerateLevel
      → buildPaths (Warnsdorff + rescue → array of vertex paths)
      → assignExits (head-end assignment → Arrow[])
  → store updated with new arrows + mode='test'

User taps arrow in test mode
  → canArrowExit (integer delta-walk through posMap)
  → if clear → added to removedInTest → arrow disappears
```
