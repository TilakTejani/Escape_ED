# Cube Arrow Maker — Logic Documentation

## Overview

This tool generates and edits levels for Escape-ƎD: a puzzle game where arrows are placed on the surface of a 3D cube grid. The player must tap arrows in the correct order to slide them off the cube. An arrow can only exit if its path ahead (in the direction it points) is clear.

There are five layers of logic:
1. **Geometry** — how the cube surface is modelled as a graph
2. **Arrow model** — what an arrow is
3. **Validation** — what makes a valid arrow placement and a solvable level
4. **Generator** — how levels are auto-generated
5. **Store** — editor state, actions, and persistence

---

## 1. Cube Geometry (`lib/cube.ts`)

### Tile-centre vertex model

The cube is an `nx × ny × nz` grid. Vertices are the **centres of surface tiles** on each face — not corner or edge points of the 3D grid. Each face is generated independently:

| Face | y position | Loop |
|---|---|---|
| Front (+Z) / Back (−Z) | z = ±Rz | nx × ny tiles |
| Top (+Y) / Bottom (−Y) | y = ±Ry | nx × nz tiles |
| Right (+X) / Left (−X) | x = ±Rx | ny × nz tiles |

where `Rx = nx/2`, `Ry = ny/2`, `Rz = nz/2`.

Tile centre coordinates are centred floats (e.g. for a 3×3×3 cube, x ∈ {−1, 0, 1}). Each vertex belongs to **exactly one face** — there are no shared corner or edge vertices.

`posMap` is a `Map<"x.xx,y.xx,z.xx", index>` for O(1) position lookup — used by both edge generation passes.

### Graph edges — two passes

**Pass 1 — same-face edges (unit-offset)**

Two vertices on the same face are connected if their centres are exactly 1 unit apart:
```
for each vertex (x, y, z):
    check all 6 unit-offset neighbours via posMap
    if neighbour exists → add edge
```

**Pass 2 — cross-face edges (border tiles)**

Tile centres on adjacent faces are ~0.707 apart (not 1.0), so Pass 1 misses them. Pass 2 adds these edges explicitly.

Rule: a border tile on face A connects to the matching border tile on the adjacent face B if they share the same coordinate along the shared-edge axis.

Example (3×3×3):
- Front face top-row tile `(x, 1, 1.5)` ↔ Top face front-row tile `(x, 1.5, 1)`

General formula for a vertex on the `+Z` face with sign `s = +1`:

| Border condition on A | B position |
|---|---|
| `ay = +(Ry − 0.5)` | `(ax, +Ry, s·(Rz−0.5))` |
| `ay = −(Ry − 0.5)` | `(ax, −Ry, s·(Rz−0.5))` |
| `ax = +(Rx − 0.5)` | `(+Rx, ay, s·(Rz−0.5))` |
| `ax = −(Rx − 0.5)` | `(−Rx, ay, s·(Rz−0.5))` |

The same pattern applies symmetrically for face axes `x` and `y`. Corner tiles (at two border conditions simultaneously) connect to two adjacent faces.

This enables arrows to travel across cube corners and is what makes cross-face paths possible.

---

## 2. Arrow Model (`types/index.ts`)

```typescript
interface Arrow {
  id: string
  path: number[]                    // ordered sequence of vertex indices
  headEnd: 'start' | 'end'         // which end of the path the arrowhead is at
  headDir: [number, number, number] // unit direction the arrowhead points (face-local plane)
}
```

An arrow is a **path** through the surface graph (no repeated vertices, adjacent vertices only). One end is the **head** (arrowhead), the other is the **tail**.

`headDir` is computed by `getExitDirection`: takes the neck→head direction vector and zeros out the face-normal component, then normalises. This gives a direction vector that lies flat on the head tile's face.

---

## 3. Validation (`lib/cube.ts`)

### Exit trajectory: `exitPathVertices`

Computes the sequence of tiles the exit ray passes through, starting from the head:

1. Set `u = tail-adjacent vertex`, `v = head vertex`
2. Each step: call `getNextStraightVertex(u, v)` — finds the neighbour of `v` (excluding `u`) with the highest dot product against the direction `u→v`
3. **Fly-off mechanic**: if the next vertex is on a **different face** (`getFaceNormalAxis` changes) → stop. The arrow has left the face and exits into 3D space
4. If `getNextStraightVertex` returns null → stop (no straight continuation exists)

The result is the list of same-face tiles the exit ray traverses before flying off.

> **Known bug**: `exitPathVertices` stops at the face boundary (fly-off). This means tiles on the *adjacent* face beyond the corner are never checked. If another arrow occupies a border tile on the adjacent face that is directly in the physical exit path of the departing arrow, `canArrowExit` does not detect the block. The arrow is incorrectly allowed to exit. This is a false-clear in the blocking check for cross-face obstructions.

### `canArrowExit`

Walks the exit trajectory step by step and checks for collisions:

- **Self-collision (snake model)**: the arrow body slithers forward as it exits. A self-collision only occurs if the head steps onto a body segment that hasn't slithered out of the way yet (checked by `stepDistance` vs path position).
- **Other-arrow node collision**: exit ray vertex occupied by another arrow's path vertex.
- **Other-arrow edge collision**: exit ray edge `(prevV → nextV)` matches an edge segment of another arrow's path.

Returns `true` (can exit) if no collision is found along the full trajectory.

### Placement constraints

- **`arrowPointsAtItself`**: exit ray from the newly placed arrow must not re-enter any of its own vertices.
- **`arrowsDirectlyFacing`**: two arrowheads cannot point directly at each other (head of A is on exit trajectory of B, AND head of B is on exit trajectory of A).

### Occupied tracking

The store caches two sets for O(1) placement validation:
- `occupiedEdges: Set<string>` — all edges used by placed arrows
- `occupiedVertices: Set<number>` — all vertices used by placed arrows

Both are incrementally updated on every mutation.

---

## 4. Auto-Generator (`lib/generator.ts`)

The generator produces a complete valid level in two phases.

### Difficulty config

| Difficulty | `facePenalty` | `preferredMaxTraj` | `preferLong` |
|---|---|---|---|
| easy | 8 | 1 | false |
| medium | 2 | 3 | false |
| hard | 0 | 999 | true |

- `facePenalty`: scoring cost per new face introduced into a path. High = arrows prefer to stay on one face (easy to trace). Zero = arrows freely cross faces (harder to follow).
- `preferredMaxTraj`: target maximum exit-trajectory length. Short = arrow exits immediately (obviously removable). Long = arrow is blocked many tiles ahead (harder to assess).
- `preferLong`: hard mode sorts path assignment to prefer longer exit trajectories.

### Phase 1: `buildPaths`

**Goal**: partition as many surface vertices as possible into valid paths (length ≥ 2), with no vertex shared between paths.

**Algorithm**: Warnsdorff-style traversal with isolated-vertex rescue.

#### Start selection (`chooseStart`)

Pick the unvisited vertex with lowest `freeDeg` (count of unvisited neighbours):
- `freeDeg ≤ 1` → use raw score (strongly prioritised — almost unreachable)
- Others → add `random() × 5` noise (avoid always starting at the same corner)

#### Step scoring (`scoreStep`)

At each extension step, score every unvisited neighbour:
```
newFaces = popcount(faceMasks[nb] & ~currentPathMask)
score    = freeDeg[nb]
         + newFaces × facePenalty × 0.5
         + random() × 3.0
         − (dot(lastDir, nb−curV) > 0.5 ? (straightness − 0.5) × 6 : 0)
```

Pick the neighbour with the **lowest** score.

`faceMasks[v]` is a 6-bit mask indicating which face vertex `v` is on (1 bit per face: +X/−X/+Y/−Y/+Z/−Z). Since each vertex is a tile centre on exactly one face, exactly 1 bit is set.

**Straightness bias**: if the candidate continues in the same direction as the last step (dot > 0.5), subtract `(straightness − 0.5) × 6`. With `straightness = 1.0`, straight steps score much lower (preferred); with `straightness = 0.0`, turns are preferred.

#### Isolated-vertex rescue

After visiting any vertex, check all its neighbours. If a neighbour `u` now has `freeDeg = 0` (all its neighbours visited, `u` itself not visited), `u` would be permanently stranded. Rescue it by grafting onto an existing committed path:

1. **Tail extension**: `u` is adjacent to a committed path's tail → append `u`
2. **Head reversal**: `u` is adjacent to a committed path's head → reverse path, append `u`
3. **Middle split**: `u` is adjacent to a middle vertex `m` of a committed path → split at `m`; left half + `u` keeps the original path slot; right half (if ≥ 2 vertices) becomes a new path

If all strategies fail, the vertex is lost (minimised by Warnsdorff scoring).

### Phase 2: `assignExits`

**Goal**: for each path produced by Phase 1, decide which end is the arrowhead such that the resulting level is solvable and matches difficulty targets.

**`ExitInfo`** — precomputed for both ends (`'start'` and `'end'`) of every path:
- `selfBlocked`: exit ray re-enters the arrow's own vertices (`arrowPointsAtItself`)
- `trajectory`: list of same-face vertices the exit ray passes through before fly-off (`exitPathVertices`)

**Sort order**: paths sorted by minimum valid (non-selfBlocked) trajectory length. `preferLong = true` (hard) reverses this, assigning longest trajectories first.

**Assignment per path (greedy)**:
1. Discard ends where `selfBlocked = true`
2. Among remaining, prefer ends where `trajectory.length ≤ preferredMaxTraj`
3. From preferred ends, pick the first whose trajectory tiles don't overlap any already-placed arrow's vertices or edges (`placedVertices` / `placedEdges`)
4. If no preferred end passes the cross-arrow check → fall back to any valid (non-selfBlocked) end, ignoring cross-arrow blocking
5. If no valid end exists at all → return `null` (trigger retry)

**Retry loop**: `autoGenerateLevel` runs up to 20 attempts, each using different random choices in `chooseStart` and `scoreStep`. Keeps the result with the most arrows.

**Fallback**: if all 20 attempts return `null`, run `buildPaths` once and assign all arrows `headEnd: 'start'` (guarantees output, though not perfectly validated).

---

## 5. Store (`store/levelStore.ts`)

The Zustand store is the single source of truth for editor state.

### State

| Field | Purpose |
|---|---|
| `gridSize` | Cube dimensions |
| `arrows` | All placed arrows |
| `geometry` | Cached `CubeGeometry` — recomputed only when `gridSize` changes |
| `occupiedEdges` | Set of all edge keys used by arrows |
| `occupiedVertices` | Set of all vertex indices used by arrows |
| `pendingPath` | Vertices clicked so far in the current arrow being drawn |
| `pendingHeadEnd` | Which end of the pending path will be the head |
| `mode` | `'add'` / `'select'` / `'test'` |
| `straightness` | Generator straightness parameter (0.0 = turns, 1.0 = straight) |
| `removedInTest` | Arrow IDs already tapped off in test mode |

### Key flows

**Drawing an arrow (add mode)**:
1. Click vertex → `addVertexToPending`: checks not occupied, not already pending, adjacent to last, edge not occupied
2. Click confirm → `confirmArrow`: validates no self-pointing, no head-on collision, updates both occupied sets

**Test mode**:
- `tapArrow(id)`: runs `canArrowExit` on the current remaining arrows. If it can exit, adds to `removedInTest`.
- `tapFirstRemovable()`: hint button — finds and removes the first arrow that can currently exit.

**Generation**:
- `generateArrows(maxPathLen, difficulty)` calls `autoGenerateLevel`, logs tile-sharing diagnostics, computes turn percentage, and auto-names the level `"3x3x3_hard_40%"`.

**Persistence**:
- Levels saved to `localStorage` under key `'escape-ed-levels'` as `SavedLevel[]`.
- `saveCurrentLevel` upserts by `currentLevelId`. New levels get a UUID.

---

## 6. Rendering (`components/CubeScene.tsx`)

Rendered with React Three Fiber (R3F).

- **`CubeFaces`**: 6 flat planes, one per cube face
- **`EdgeLine`**: one line per graph edge, with a fat invisible hit box. Clicking an edge mid-path auto-selects the unvisited endpoint
- **`VertexDot`**: one sphere per vertex. Colour-coded: grey (free), green (reachable from current path end), purple (in pending path), cyan (last pending vertex)
- **`ArrowMesh`**: line + arrowhead cone per placed arrow. In test mode, red cone = blocked, can't exit yet

### Corner routing (`getRenderPoints`)

When two consecutive path vertices are on different faces (`getFaceNormalAxis` differs), `getRenderPoints` inserts a **corner waypoint** at the cube's geometric edge (e.g. `(x, Ry, Rz)`) so the rendered line hugs the surface instead of cutting through the interior. This is purely visual — it does not affect the graph or validation logic.

---

## Data flow summary

```
User clicks vertex
  → addVertexToPending (adjacency + occupancy check)
  → pendingPath grows

User confirms
  → confirmArrow (self-point + head-on check)
  → arrow added, occupiedEdges + occupiedVertices updated

User clicks Generate
  → autoGenerateLevel
      → buildPaths (Warnsdorff + rescue → array of vertex paths)
      → assignExits (head-end assignment → Arrow[])
      → retry up to 20× keeping best result
  → store updated with new arrows + mode='test'

User taps arrow in test mode
  → canArrowExit (exitPathVertices → same-face collision walk)
  → if clear → added to removedInTest → arrow disappears
```

---

## Known issues

### Exit check: cross-face blocking not detected

**Location**: `exitPathVertices` in `lib/cube.ts`

**Description**: The fly-off mechanic stops the exit trajectory at the face boundary — the exit ray never crosses onto an adjacent face. With cross-face edges now present in the graph, another arrow may occupy a border tile on the adjacent face that is physically in the departing arrow's path around the corner. `canArrowExit` does not detect this obstruction and incorrectly returns `true`.

**Impact**: In test mode, an arrow may be tappable when it should be blocked. Levels may appear solvable in orderings that should not be valid.

**Fix needed**: `exitPathVertices` (and the collision walk in `canArrowExit`) should continue across face boundaries, following the same dot-product straight-line logic, rather than treating the face boundary as an immediate fly-off.
