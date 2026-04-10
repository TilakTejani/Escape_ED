# Cube Arrow Maker — Changelog

## Overview of Changes

---

## `types/index.ts`

### Added `headDir` to `Arrow`
```ts
headDir: [number, number, number]
```
Stores the grid-space direction vector the arrowhead points (from tail vertex to head vertex). Used by the Unity game to orient arrows correctly without re-deriving direction from path order.

---

## `lib/generator.ts` — Full Rewrite

### Old algorithm (removed)
Backtracking partition + greedy partition fallback. The backtracking was NP-hard and timed out on large grids (5×5×5+). The greedy fallback didn't use the `straightness` setting.

### New algorithm: Greedy head-first + isolated-vertex rescue

**`buildPaths()`** — replaces `backtrackPartition` and `greedyPartition`

1. **Warnsdorff start selection** — `chooseStart()` picks the unvisited vertex with the fewest free neighbours. Truly constrained vertices (freeDeg ≤ 1) get Warnsdorff priority; all others get heavy noise (`freeDeg + Math.random() * 8`) to avoid always starting at the same corner and producing zigzag patterns.

2. **Greedy body extension** — `scoreStep()` scores each candidate next vertex:
   - Base score = `freeDeg[nb]` (Warnsdorff)
   - Face penalty = `newFaces * facePenalty * 0.5` (difficulty-tuned)
   - Noise = `Math.random() * 3.0` (breaks deterministic zigzag)
   - Straightness bias = `±(straightness - 0.5) * 6` (makes the Path Style slider actually work)

3. **Isolated-vertex rescue** — after visiting any vertex, every neighbour is checked. If a neighbour's `freeDeg` drops to 0 (unreachable), `rescueVertex()` runs:
   - **Pass 1:** extend a committed path's tail or reversed head to absorb the isolated vertex
   - **Pass 2:** split a committed path at the middle vertex adjacent to it; left half keeps the middle + isolated vertex; right half becomes a new path or is merged into a neighbour if singleton
   - Never backtracks, always terminates O(V·E)

4. **Singleton handling** — paths of length 1 after `buildPaths` are merged into the tail of an adjacent committed path instead of being discarded.

**`assignExits()`** — unchanged in purpose, now includes `headDir` in output arrows.

**`fallbackAssign()`** — used after 20 failed `assignExits` attempts; assigns any geometric exit ignoring cross-arrow blocking. Now also includes `headDir`.

**`computeHeadDir()`** — new helper. Derives `[dx, dy, dz]` from the tail→head vertex pair in grid coords.

**`autoGenerateLevel()`** — 20 random attempts, then `fallbackAssign`. No deadline or timeout needed.

---

## `store/levelStore.ts`

### `generateArrows()`
- Computes **turn percentage** after generation: `turns / segments * 100` across all arrows
- Sets `currentLevelName` automatically: `{x}x{y}x{z}_{difficulty}_{turnPct}%` (e.g. `3x3x3_medium_42%`)
- Resets `currentLevelId` to `null` (generated levels are unsaved)
- Switches `mode` to `'test'` immediately after generation

### `confirmArrow()`
- Computes and stores `headDir` on manually placed arrows, matching the generated arrow format

### `loadLevel()`
- Switches `mode` to `'test'` (not `'add'`) after loading — loaded levels should be tested, not edited
- Backfills `headDir` for saved levels that predate the field

### `importLevel()`
- Backfills `headDir` for imported JSON files that predate the field

### `saveCurrentLevel()`
- Simplified: always saves with `currentLevelName` — no name prompt on first save
- If `currentLevelId` is set, overwrites the existing entry; otherwise creates new

---

## `components/GridSizePanel.tsx`

### Auto Solve + Reset moved below Generate Puzzle
Auto Solve and Reset buttons only appear when `mode === 'test' && arrows.length > 0`, directly below the Generate Puzzle button. Previously they were in `LeftPanel`.

### No confirm dialog on regenerate
`handleGenerate` no longer shows `confirm()` before generating. Clicking Generate Puzzle immediately regenerates.

### Max Path Length slider
- Bounded to `25%` of edge count (`geometry.edges.length * 0.25`) to prevent degenerate paths
- Defaults to `sqrt(edge count)` as a sensible midpoint
- Resets to the new default whenever grid size changes

### Path Style (straightness) slider
- Now correctly wired through to `autoGenerateLevel` via `generateArrows(genMaxLen, difficulty)`
- The `straightness` value feeds into `scoreStep`'s bias term, actually producing straighter or more turned arrows

### Turn Rate stat
Shows average turn rate across all placed arrows, displayed below the arrow count in the Stats section.

---

## `components/LevelsPanel.tsx`

### Turn Rate + Coverage stats
Shown below the current level name whenever arrows exist:
- **Turn Rate**: `turns / segments * 100`, rounded to integer percent
- **Coverage**: `covered vertices / total vertices` (e.g. `18/26`)
Both computed with `useMemo` and update live as arrows change.

### Download button per saved level
Each row in the saved levels list has a `↓` download button (visible on hover) that triggers a JSON blob download of that level's data. Filename: `{levelName}.json`.

### Save button simplified
Always shows "Save". Calls `saveCurrentLevel()` directly with `currentLevelName` — no name prompt. The level name is auto-set by generation (see above) or can be changed via the rename action on saved rows.

---

## `components/CubeScene.tsx`

### Test mode: uncovered vertex dots
In `mode === 'test'`, vertices not part of any remaining arrow are rendered as **orange spheres** (`#f97316`, radius 0.07, emissive). These represent "uncovered" surface points and help visually verify full coverage.

### Add mode: occupied vertices hidden
Vertices already part of a committed arrow return `null` in add mode. The arrow body visually marks them, so the vertex dot is redundant and adds clutter.
