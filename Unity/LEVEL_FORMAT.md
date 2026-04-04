# Level Format

Levels are defined as JSON files assigned to `LevelManager.levelJsonFile` (a `TextAsset`).

---

## Schema

```json
{
  "gridSize": {
    "x": 3,
    "y": 3,
    "z": 3
  },
  "arrows": [
    {
      "id": "arrow_0",
      "path": [0, 1, 2, 7],
      "headEnd": "end"
    },
    {
      "id": "arrow_1",
      "path": [6, 3, 4, 5],
      "headEnd": "start"
    }
  ]
}
```

---

## Fields

### `gridSize`

Dimensions of the cube grid.

| Field | Type | Description |
|---|---|---|
| `x` | int | Width |
| `y` | int | Height |
| `z` | int | Depth |

The grid generates all **surface** dots only (face, edge, and corner positions). Interior positions are excluded.

Total dot count for an N×N×N grid = `N³ - (N-2)³`.

---

### `arrows[]`

Array of arrow definitions.

| Field | Type | Description |
|---|---|---|
| `id` | string | Unique identifier (used for debugging) |
| `path` | int[] | Ordered list of dot indices (minimum 2) |
| `headEnd` | string | Which end has the arrowhead: `"end"` or `"start"` |

#### `path`

Each integer is a **dot index** into the grid's surface dot list. The list is built in ZYX order (z outer, y middle, x inner), skipping interior positions.

To find a dot's index: use the Level Maker tool, which displays indices on the grid.

#### `headEnd`

- `"end"` — arrowhead is at `path[last]` (default, path order = tail to head)
- `"start"` — arrowhead is at `path[0]` (path is reversed before rendering)

---

## Dot Index Ordering

Dots are indexed in this order:

```
for z in 0..gridSize.z:
  for y in 0..gridSize.y:
    for x in 0..gridSize.x:
      if isSurface(x, y, z): add dot
```

A position is a **surface** dot if it sits on at least one face of the cube:
- `x == 0` or `x == gridSize.x - 1`
- `y == 0` or `y == gridSize.y - 1`
- `z == 0` or `z == gridSize.z - 1`

---

## Dot Types

The grid automatically classifies each dot:

| Type | Condition | Normals |
|---|---|---|
| `Face` | On exactly 1 face | 1 normal |
| `Edge` | On exactly 2 faces | 2 normals |
| `Corner` | On exactly 3 faces | 3 normals |

Dot type determines how the arrow mesh is rendered at that point:
- **Face** → flat quad
- **Edge** → two quads folded across the edge
- **Corner** → folded cap spanning all three faces

---

## Example: 3×3×3 Grid

A 3×3×3 grid has 26 surface dots (all positions except the center).

```
Dot 0  = (0,0,0) — corner
Dot 1  = (1,0,0) — edge
Dot 2  = (2,0,0) — corner
Dot 3  = (0,1,0) — edge
Dot 4  = (1,1,0) — face
Dot 5  = (2,1,0) — edge
...
```

An arrow `"path": [0, 1, 2]` draws from corner (0,0,0) along the bottom front edge to corner (2,0,0), with arrowhead at dot 2.

---

## Validation Rules

- `path` must have at least **2** dots.
- Path dots should form a **connected surface path** — gaps or interior jumps produce visual artefacts.
- Adjacent path dots should be **one grid step apart** for correct spacing and animation.
- Duplicate indices in a path are not supported.
