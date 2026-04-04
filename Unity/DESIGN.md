# ESCAPE-ƎD: Technical Design

This document covers the architecture, algorithms, and contracts that power the arrow system.

---

## Module Map

```
Arrow.cs  (MonoBehaviour, IInteractable)
  │
  ├── ArrowMeshBuilder.cs  (static)
  │     Builds all mesh geometry from a path:
  │     body quads, tail cap, bends, tip
  │
  ├── ArrowAnimator.cs  (instance, owns coroutines)
  │     EjectSequence — path-buffer snake slide off cube
  │     BlockedShakeSequence — push/retract snake motion
  │
  └── ArrowPhysicsHandler.cs  (instance, owns colliders)
        Pooled BoxColliders for body segments
        Convex MeshCollider for arrowhead tip

CubeGrid.cs
  Provides: dot world positions, face normals, DotType

LevelManager.cs
  Loads JSON → spawns Arrow instances → handles interactions

GhostCubeController.cs
  Per-face alpha: fades cube faces and arrow segments
  that are pointing directly at the camera
```

---

## Coordinate Space Contract

**Rule: `originalPositions` is always stored in LOCAL space** (relative to the Arrow's transform, which is the same as the cube's transform since the arrow has identity local transform).

| Location | Space | Reason |
|---|---|---|
| `grid.GetWorldPosByIndex()` | World | Callers need world positions for SetPath |
| `SetPath(positions, ...)` | World | External API accepts world |
| `ctx.localPos` | Local | Computed by CreateBuildContext via InverseTransformPoint |
| `originalPositions` | Local | Stored from `ctx.localPos` after CreateBuildContext |
| `GetEjectionData()` | World out | Uses TransformPoint(localPos) → world |
| `EjectSequence pathBuffer` | World | Uses TransformPoint(originalPositions[i]) → world |
| `BlockedShakeSequence pathBuffer` | World | Same as above |

Why local storage: the cube rotates. If world positions were stored, `TransformPoint` in the animators would double-transform them after any rotation, producing wrong pathBuffer positions and a degenerate or invisible mesh during ejection.

---

## SetPath Pipeline

Called every frame during animation, and once at level load per arrow.

```
SetPath(worldPositions, allNormals, dotTypes)
  │
  ├── CreateBuildContext()
  │     - Convert world → local (InverseTransformPoint)
  │     - Compute cumulative distances for UV mapping
  │     - Compute totalDist (includes tip length for Face tips)
  │     - Store ctx.localPos, ctx.dist, ctx.totalDist
  │
  ├── [Snapshot if !isEjecting && !isAnimating]
  │     originalPositions = ctx.localPos   ← LOCAL SPACE
  │     originalNormals   = DeepCopy(allNormals)
  │     originalDotTypes  = copy(dotTypes)
  │
  ├── ArrowMeshBuilder.BuildBody(ctx)
  ├── ArrowMeshBuilder.BuildTailCap(ctx)
  ├── ArrowMeshBuilder.BuildBends(ctx)
  ├── ArrowMeshBuilder.BuildTip(ctx, out tipVerts)
  ├── ArrowMeshBuilder.FinalizeMesh(mesh, ctx, smoothShading)
  │
  └── [if !isEjecting]
        ArrowPhysicsHandler.UpdateSegmentColliders(...)
        ArrowPhysicsHandler.UpdateTipCollider(tipVerts)
```

---

## Mesh Building

### DotType and Normals

Each dot has a `DotType` (Face / Edge / Corner) and a list of face normals:

| DotType | Normals | Meaning |
|---|---|---|
| Face | [up] | Interior of one cube face |
| Edge | [up, right] | Shared by two faces |
| Corner | [up, right, forward] | Shared by three faces |

The normal lists are in the arrow's **local space** (axis-aligned: Vector3.up, Vector3.left, etc.). They are never rotated — they always describe which faces of the cube the dot sits on.

### Body Segments

For each consecutive pair of dots (i, i+1):

1. Check `IsEdgeSegment(normals[i], normals[i+1])` — both dots on the same cube edge?
   - **Yes** → `AddFoldQuads`: two quads, one per face, sharing miter-corrected edge vertices
   - **No** → single flat quad on the primary face normal

2. Last segment is trimmed by `tipLength` if tip is not a Face dot (so arrowhead doesn't overlap body).

### Edge Fold Geometry (AddFoldQuads)

```
Edge line (miter-corrected lift)
  ├── Face 1 quad: edge → InwardDir(n1, dir, n2) × halfWidth + n1 lift
  └── Face 2 quad: edge → InwardDir(n2, dir, n1) × halfWidth + n2 lift
```

`InwardDir(faceNormal, edgeDir, otherFaceNormal)`:
- Compute `Cross(faceNormal, edgeDir)` to get a direction perpendicular to the edge in the face plane
- Negate if it points toward `otherFaceNormal` (we want it pointing AWAY, into the face)

`GetCorrectedLift(normals, offset)`:
- Bisects all face normals to find the correct lift direction at a multi-face vertex
- Scales the bisector so its projection onto any face normal equals exactly `offset`
- Prevents mesh dipping at cube seams

### Tip Geometry

| Tip Dot Type | Shape |
|---|---|
| Face | Forward-pointing triangle (apex ahead of tipPos) |
| Edge (last seg on edge) | Two triangles via `AddFoldTip`, one per face, apex at tipPos |
| Edge (last seg on face) | Single reversed triangle, apex at tipPos |

### Round Caps (AddFoldedRoundCap)

Used at the tail and at non-face interior bends.

For each face the dot sits on:
1. Build a 2D orthonormal basis in the face plane
2. Sample the full circle (2π) uniformly, plus exact seam angles where adjacent faces meet (`InsertAngle`)
3. Exclude samples that would cross into another face (`dot(dir, otherNormal) > 0.02`)
4. Apply miter-corrected lift at seam vertices so adjacent face arcs share identical 3D boundary points

This produces a perfectly matched multi-face round cap with no gap or overlap at seams.

### Bend Joins

At face-interior bends:
- **Outer arc** (`AddBendArc`): fills the convex gap between two segment ends
- **Inner bevel triangle**: fills the concave gap at the inner corner

---

## Animation System

### Path Buffer (both animations)

Both EjectSequence and BlockedShakeSequence use the same path-buffer pattern:

```
pathBuffer = [dot_0_world, dot_1_world, ..., dot_n_world]  ← initial positions

Each frame:
  pathBuffer.Add(newHeadPos)                   ← extend by one step
  SamplePathBufferAll(pathBuffer, n, gridStep) ← sample n evenly-spaced positions
  SetPath(sampledPositions, ...)               ← rebuild mesh
```

`SamplePathBufferAll` is O(buffer + n) — single backwards pass, emits all n positions simultaneously.

The head is always `pathBuffer.Last()`. Each dot i is at distance `i × gridStep` behind the head along the buffer. This creates genuine temporal lag: the tail follows the exact path the head traced.

### EjectSequence

1. Build initial pathBuffer from `originalPositions` (local → world via TransformPoint)
2. Compute `headDir` from last two buffer entries
3. Detach arrow from cube (`SetParent(null, false)`)
4. Phase 1: extend buffer by `headDir × step` each frame until traveled `(n+10) × gridStep`
5. Phase 2: accelerate arrow transform position off-screen
6. Destroy GameObject

### BlockedShakeSequence

1. Build pathBuffer from `originalPositions` (local → world)
2. Phase A (push): extend buffer forward by `headDir` for `0.5 × gridStep`
3. Phase B (retract): extend buffer backward by `-headDir` for the same distance
4. Restore exact original positions via `SetPath(originalPositions, ...)`

`isAnimating` flag prevents `SetPath` from overwriting the snapshot during the animation.

---

## Collision System

### Body Colliders (ArrowPhysicsHandler)

Pooled `BoxCollider` GameObjects, one per body segment. Each collider:
- `localPosition` = segment midpoint (local space)
- `localRotation` = `LookRotation(segDir, faceNormal)`
- `size` = `(lineWidth, lineWidth, segmentLength)`
- `center` = `(0, surfaceOffset, 0)` (lifted off the surface)

Colliders are deactivated during ejection (`isEjecting` guard in `SetPath`). Unused pool slots are disabled, not destroyed.

### Tip Collider

A convex `MeshCollider` built from `tipVerts` (the arrowhead geometry vertices). Rebuilt each `SetPath` call using fan triangulation from vertex 0. The mesh is reused (`.Clear()` + re-set), marked `Dynamic`.

### Blocking Detection (LevelManager.IsArrowBlocked)

Uses `Physics.OverlapCapsuleNonAlloc` along the arrow's eject path:

```
p1 = tipPos + faceNormal × surfaceOffset
p2 = p1 + tipDir × (maxGridDimension × spacing)
radius = lineWidth × 0.5 × 0.95   ← 5% margin for corner-kiss
layer = LayerMask.GetMask("Arrow") ← computed at runtime, not inspector field
```

A hit is valid if:
- The collider is NOT a child of the ejecting arrow (own body)
- The collider has an `Arrow` parent component
- That arrow is not the same as the ejecting one
- That arrow is not already ejecting

---

## Ghost Cube (GhostCubeController)

Runs in `LateUpdate` each frame.

**Face transparency**: For each cube face, computes `dot(worldNormal, viewDir)`. High dot (face pointing at camera) → low alpha (`minAlpha`). Applies via `MaterialPropertyBlock` — no material instancing.

**Arrow face-fading**: The arrow shader receives `_FaceAlphas0` (faces 0-3: up, down, left, right) and `_FaceAlphas1` (faces 4-5: forward, back) as Vector4 uniforms per frame. Each face alpha is 1.0 if the face is visible from camera, `minArrowAlpha` if pointing away. The shader uses the vertex's UV2 (which encodes face index via `FaceIndexFromNormal`) to select the correct alpha.

This makes arrows on faces pointing away from the camera fade out, creating the "see through the cube" effect without any geometry changes.

---

## Layer Setup (Required)

| Layer Name | Used For |
|---|---|
| `Arrow` | All active arrows (colliders + raycasting) |
| `EjectingArrow` | Arrows during ejection (ignored in blocking check, IgnoreLayerCollision with self) |

Both layers must be defined in **Edit → Project Settings → Tags and Layers**.

`Physics.IgnoreLayerCollision(EjectingArrow, EjectingArrow, true)` is set automatically at startup via `[RuntimeInitializeOnLoadMethod]` in `Arrow.cs`.

---

## Known Limitations

1. **`arrowEjectMaterial`** is declared and exposed in the Inspector but never applied programmatically during ejection. The arrow keeps using `arrowMaterial` while ejecting, meaning GhostCubeController face-fading still applies to it.

2. **`GetCorrectedLift` allocates** a temporary `List<Vector3>` in the two-normal overload. Called frequently during mesh building.

3. **`uv2s` array allocates** every `FinalizeMesh` call (`new Vector2[meshNormals.Count]`). During animation, this runs every frame.
