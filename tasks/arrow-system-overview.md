# Arrow System — Complete Technical Overview

This document covers the full architecture of the Escape-ƎD arrow system: how data flows from a level JSON to a rendered, interactive, animatable mesh on a rotating cube.

---

## Table of Contents

1. [High-Level Architecture](#1-high-level-architecture)
2. [Data Flow — Level Load to Rendered Arrow](#2-data-flow)
3. [CubeGrid & DotType](#3-cubegrid--dottype)
4. [Coordinate Space Contract](#4-coordinate-space-contract)
5. [Arrow.cs — State Machine & Orchestrator](#5-arrowcs--state-machine--orchestrator)
6. [ArrowMeshBuilder — Geometry Pipeline](#6-arrowmeshbuilder--geometry-pipeline)
7. [ArrowPulsing Shader — Transparency](#7-arrowpulsing-shader--transparency)
8. [ArrowPhysicsHandler — Colliders](#8-arrowphysicshandler--colliders)
9. [InteractionSystem — Tap to Collider to Arrow](#9-interactionsystem--tap-to-collider-to-arrow)
10. [ArrowAnimator — Eject & Blocked Shake](#10-arrowanimator--eject--blocked-shake)
11. [ArrowConstants — Tuning Reference](#11-arrowconstants--tuning-reference)
12. [Materials — ArrowPulseMat & ArrowEjectMat](#12-materials)
13. [Known Pitfalls & Hard-Won Rules](#13-known-pitfalls--hard-won-rules)

---

## 1. High-Level Architecture

Four files own the arrow system. Each has one job:

| File | Role |
|---|---|
| `Arrow.cs` | MonoBehaviour orchestrator, state machine, public API |
| `ArrowMeshBuilder.cs` | Static mesh geometry — body quads, tip, tail cap, bend arcs |
| `ArrowAnimator.cs` | Eject and blocked-shake coroutines, path buffer sampling |
| `ArrowPhysicsHandler.cs` | BoxCollider pool + convex tip MeshCollider |

Supporting files:

| File | Role |
|---|---|
| `ArrowConstants.cs` | All numeric constants — shape, speed, layers |
| `ArrowPulsing.shader` | URP transparent shader — normal-based alpha, UV pulse |
| `InteractionSystem.cs` | Raycast → face-normal guard → `IInteractable.OnInteract()` |
| `CubeGrid.cs` | Generates dot grid, provides world positions and face normals per index |
| `LevelManager.cs` | Parses JSON, spawns arrow prefabs, calls `SetPath` |
| `GhostCubeController.cs` | Drives cube face alpha; sets `_MinArrowAlpha` global shader float |

---

## 2. Data Flow

```
JSON (levelJsonFile)
  └─ LevelManager.LoadLevelFromJSON()
       └─ foreach arrowData:
            ├─ grid.GetWorldPosByIndex(index)   → worldPath  (world space Vector3s)
            ├─ grid.GetAllFaceNormals(index)     → allNormals (local-space List<Vector3>)
            ├─ grid.GetDotType(index)            → dotTypes   (Face / Edge / Corner)
            └─ Arrow.SetPath(worldPath, allNormals, dotTypes, useWorldSpace:true)
                 ├─ ProjectToLocal() via InverseTransformPoint
                 ├─ Snapshot → originalPositions / originalNormals / originalDotTypes
                 ├─ ArrowMeshBuilder.Build*()    → mesh written to MeshFilter
                 └─ ArrowPhysicsHandler.Update*() → colliders placed
```

`SetPath` is the single entry point for every mesh update — initial placement, animation frames, restore after shake, and each frame of ejection.

---

## 3. CubeGrid & DotType

`CubeGrid` generates a 3D lattice of dot positions on the cube surface. Each dot has an index and a coordinate (`Vector3Int`).

### DotType

```csharp
public enum DotType { Face, Edge, Corner }
```

| Type | Meaning | Normal count |
|---|---|---|
| `Face` | Dot on a flat cube face | 1 |
| `Edge` | Dot on a cube edge (shared by 2 faces) | 2 |
| `Corner` | Dot at a cube corner (shared by 3 faces) | 3 |

`GetAllFaceNormals(index)` returns the appropriate normals in **local space** (axis-aligned vectors like `Vector3.up`, `Vector3.right`).

### Key grid methods

```csharp
grid.GetWorldPosByIndex(index)  // True world space. Uses TransformPoint internally.
grid.GetAllFaceNormals(index)   // Local-space face normals for the dot.
grid.GetDotType(index)          // Face / Edge / Corner.
```

`CalculateWorldPos` alone returns cube-local space. Always use `GetWorldPosByIndex` when world positions are needed.

---

## 4. Coordinate Space Contract

**Critical — violating this causes ghost paths and invisible meshes.**

### The rule

`originalPositions` is stored in **local space** (Arrow's transform = cube's transform at the time of spawning).

| Data | Space | How |
|---|---|---|
| `worldPath` from `LevelManager` | World | `grid.GetWorldPosByIndex` |
| `SetPath` input positions | World (default) | Passed as-is |
| `ctx.localPos` in `CreateBuildContext` | Local | `InverseTransformPoint` per point |
| `originalPositions` snapshot | Local | Copied from `ctx.localPos` |
| `allNormals` from `CubeGrid` | Local | Axis-aligned (`Vector3.up` etc.) |
| Shader input | Object space → world | `TransformObjectToWorldNormal` in shader |

### Why local storage matters

`ArrowAnimator` calls `transform.TransformPoint(originalPositions[i])` to get world positions for animation. If `originalPositions` were world positions, this would double-transform after any cube rotation.

`ArrowMeshBuilder` works entirely in local space through `ctx.localPos`. Mixing in any world-space vector (like `lastValidDir` derived from world positions) causes misalignment after cube rotation.

```csharp
// CORRECT — uses localPos
Vector3 preTipDir = (localPos[n - 1] - localPos[n - 2]).normalized;

// WRONG — positions may be world-space, corrupt after cube rotation
Vector3 preTipDir = (positions[n - 1] - positions[n - 2]).normalized;
```

### After ejection detach

`SetParent(null, worldPositionStays:true)` preserves the arrow's world rotation. Local normals remain valid — the arrow transform now carries the cube's rotation, so `TransformObjectToWorldNormal` in the shader still produces correct world normals.

---

## 5. Arrow.cs — State Machine & Orchestrator

### State flags

```csharp
private bool isEjecting  = false;
private bool isAnimating = false;
```

Guards:
- `SetPath` only snapshots `originalPositions` when `!isEjecting && !isAnimating`
- Physics colliders only update when `!isEjecting` (prevents re-enabling colliders during animation)
- `OnInteract` ignores taps when `isEjecting`

### Public API

```csharp
SetPath(positions, allNormals, dotTypes, useWorldSpace = true)
Eject()
PlayBlockedAnimation()
GetEjectionData(out tipPos, out tipDir, out faceNormal)
event Action<Arrow> OnInteractionTriggered
```

### Eject flow

```
Arrow.Eject()
  ├─ isEjecting = true
  ├─ DisableAllColliders()
  ├─ switch material to arrowEjectMaterial
  └─ StartCoroutine(ArrowAnimator.EjectSequence(...))
        └─ (see Section 10)
```

### Material switch on eject

`arrowEjectMaterial` is **required** for correct ejection rendering. Without it, the arrow uses `ArrowPulseMat` whose transparency formula fades based on frozen cube normals — as the arrow moves through space the normals misalign with the camera and the arrow goes transparent mid-flight.

`arrowEjectMaterial` is a duplicate of `ArrowPulseMat` with `_MinArrowAlpha = 1.0`, making `lerp(1, 1, t) = 1` always — fully opaque regardless of camera angle.

---

## 6. ArrowMeshBuilder — Geometry Pipeline

### Build pipeline (called in order from `SetPath`)

```
BuildBody(ctx)      → body quads between each consecutive dot pair
BuildTailCap(ctx)   → rounded cap at dot[0]
BuildBends(ctx)     → arc fills at interior turn points
BuildTip(ctx)       → arrowhead at dot[n-1]
FinalizeMesh(...)   → upload to GPU (SetVertices / SetTriangles / SetNormals)
```

All geometry is built in **local space** (`ctx.localPos`), with per-vertex normals set explicitly (no `RecalculateNormals` unless `smoothShading = true`).

### Context object

`ArrowMeshBuilder.Context` carries all inputs and shared scratch buffers. Constructed by `Arrow.CreateBuildContext`. Buffers (`_verts`, `_tris`, `_uvs`, `_meshNormals`) are reused across frames to avoid allocations.

### BuildBody

Iterates `n-1` segments. For each:
- **Edge segment** (`IsEdgeSegment` → true): calls `AddFoldQuads` — two quads, one per face, meeting at the miter edge with corrected lift.
- **Face segment**: single quad using `GetSafeRight(faceN, dir)` for the right vector. `GetSafeRight` falls back to the least-parallel world axis if `Cross(faceN, dir)` collapses.

### BuildTailCap — `AddFoldedRoundCap`

Draws a semicircular fan for each face in the dot's normal list:
1. Builds a 2D basis per face (`basisU`, `basisV`)
2. Generates arc points at `2π / segments` intervals
3. Inserts exact seam angles at face boundaries using `InsertAngle` / `Atan2Basis`
4. Filters arc points that belong to this face (rejects points that cross into another face's half-space)
5. Miter-lifts seam vertices using `GetCorrectedLift`

### BuildBends

For each interior dot (index 1 to n-2):

| Condition | Action |
|---|---|
| Both adjacent segments are fold segs AND dot is not Corner | Skip (fold quads already cover it) |
| Dot is Edge or Corner | `AddFoldedRoundCap` |
| Dot is Face, segments are nearly straight | Skip |
| Dot is Face, turn detected | `AddBendArc` (outer) + inner triangle |

**Arc winding** depends on `angleTo` sign:
```csharp
if (angleTo < 0f)
    tris.AddRange(new[] { centerIdx, arcStart + s + 1, arcStart + s });
else
    tris.AddRange(new[] { centerIdx, arcStart + s, arcStart + s + 1 });
```

**Inner triangle winding** is computed, not hardcoded — `outsideSign` flips between left and right turns.

### BuildTip

| Condition | Tip type |
|---|---|
| `dotTypes[n-1] == Face` | Standard flat triangle: apex forward, base with `GetSafeRight` |
| Last segment is edge segment | `AddFoldTip` — two triangles, one per face, wings use `InwardDir` |
| Last segment is non-edge, dot is Edge/Corner | Fallback flat triangle using previous segment's face |

### GetCorrectedLift

For multi-face vertices (edge/corner), lifts along the bisector of all face normals, scaled to maintain constant surface clearance:
```csharp
bisector = normalize(sum of normals)
lift = bisector * (offset / dot(normals[0], bisector))
```

### InwardDir

Returns the direction perpendicular to both `faceNormal` and `edgeDir`, pointing **away** from `otherFaceNormal`:
```csharp
Vector3 d = Cross(faceNormal, edgeDir).normalized;
if (Dot(d, otherFaceNormal) > 0) d = -d;
return d;
```
The sign check is critical — inverting it points toward the other face, reversing the fold.

---

## 7. ArrowPulsing Shader — Transparency

**File:** `Unity/Assets/Shaders/ArrowPulsing.shader` (URP unlit transparent)

### Alpha formula

```glsl
float3 cubeToCam = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
float  d         = dot(normalize(input.normalWS), cubeToCam);
float  t         = saturate(d / 0.2 + 1.0);
finalColor.a     = lerp(_MinArrowAlpha, 1.0, t);
```

- `d = 1` when face points directly at camera → fully opaque
- `d = -1` when face points directly away → alpha = `_MinArrowAlpha`
- `d = -0.2` is the fade threshold (fully opaque above this)

### `_MinArrowAlpha`

| Value | Effect |
|---|---|
| `0.08` (default `ArrowPulseMat`) | Near-invisible back faces — correct for cube display |
| `1.0` (`ArrowEjectMat`) | Always fully opaque — required during ejection |

`GhostCubeController` sets `_MinArrowAlpha` once per frame via `Shader.SetGlobalFloat`. This is the only arrow-related call it makes.

### UV pulse

`_PulseSpeed` and `_PulseAmplitude` drive a UV-scrolling pulse animation along the arrow body. UV.x = arc-length / totalLength (0 at tail, 1 at tip).

---

## 8. ArrowPhysicsHandler — Colliders

Manages two types of colliders:

### Segment BoxColliders

One `BoxCollider` per body segment (dot[i] → dot[i+1]), pooled in `_segmentColliders`.

Each collider child also has `ArrowSegmentFace`:
```csharp
public class ArrowSegmentFace : MonoBehaviour {
    public Vector3 localFaceNormal;
}
```

This stores the **true arrow face normal** (local space) because `hit.normal` from `Physics.Raycast` against a BoxCollider is unreliable — the ray hits the camera-facing underside of the box even when the arrow faces away from camera.

Orientation: `Quaternion.LookRotation(dir, faceN)` — forward along segment, up along face normal.

Size: `(lineWidth, lineWidth, segmentLength)` with `center.y = surfaceOffset`.

`localFaceNormal` is updated every `UpdateSegmentColliders` call in case the path changes faces.

### Tip MeshCollider

Convex `MeshCollider` built from `tipVerts` output by `BuildTip`. Rebuilt each frame when not ejecting. Includes a non-lifted base point to guarantee 3D volume (avoids PhysX coplanar errors).

### Layer management

- Arrows on layer `Arrow` while idle
- Layer switches to `EjectingArrow` at ejection start
- `Physics.IgnoreLayerCollision(ejectLayer, ejectLayer, true)` prevents ejecting arrows from colliding with each other (set up in `Arrow.SetupPhysics`)

### Ejection guard

`Arrow.SetPath` skips collider updates when `isEjecting`:
```csharp
if (!isEjecting) {
    _physics.UpdateSegmentColliders(...);
    _physics.UpdateTipCollider(...);
}
```

Without this guard, each frame of `EjectSequence` re-enables the colliders that were just disabled.

---

## 9. InteractionSystem — Tap to Collider to Arrow

**File:** `Unity/Assets/Scripts/Input/InteractionSystem.cs`

### Flow

```
InputController.OnTap (screen position, fingerId)
  └─ HandleTap()
       ├─ IsPointerOverUI(fingerId) → return if true (UI absorbs tap)
       ├─ Camera.ScreenPointToRay(screenPosition)
       ├─ Physics.Raycast(ray, interactableLayer)
       ├─ ArrowSegmentFace face-normal check (back-face guard)
       └─ IInteractable.OnInteract()
```

### Back-face guard

```csharp
var segFace = hit.collider.GetComponent<ArrowSegmentFace>();
Vector3 faceNormalWS = segFace != null
    ? hit.collider.transform.parent.TransformDirection(segFace.localFaceNormal)
    : hit.normal; // tip MeshCollider — hit.normal IS correct for mesh triangles

if (Vector3.Dot(faceNormalWS, -ray.direction) <= 0f) return;
```

- `ArrowSegmentFace.localFaceNormal` is in local space of the Arrow (parent of the collider child)
- `transform.parent.TransformDirection(...)` converts it to world space
- `Dot(faceNormalWS, -ray.direction) <= 0` means the face points away from camera → reject

The tip `MeshCollider` has no `ArrowSegmentFace` component. For a convex mesh, `hit.normal` is the mesh triangle normal, which correctly reflects face orientation.

### InputController / InputReader

`InputController` fires `OnTap(Vector2 screenPos, int fingerId)`. `InputReader` normalizes mouse/touch finger IDs (mouse = `-1`). UI protection uses `EventSystem.current.IsPointerOverGameObject(fingerId)`.

---

## 10. ArrowAnimator — Eject & Blocked Shake

### EjectSequence

```
1. CAPTURE  — TransformPoint(originalPositions[i]) → worldPositions (before detach)
              Normals stay local — NOT converted to world (would double-transform)
2. DETACH   — SetParent(null, worldPositionStays:true)
              Layer → EjectingArrow
3. BUFFER   — pathBuffer initialized with worldPositions
              Ghost point prepended one gridStep behind tail (prevents bunching on frame 0)
4. SNAKE    — M = (n-1)*S+1 sub-samples at subStep=gridStep/S spacing
              All M samples use exitNormal = originalNormals[n-1][0]
              Each frame: extend buffer head by speed*dt, trim tail, SamplePathBufferAll → SetPath
5. FINALE   — Rebuild as proper n-point arrow with arrowhead
              Accelerate along headDir for EJECT_FINAL_DURATION seconds
6. DESTROY  — Object.Destroy after finale
```

**Exit normal rule:** All snake segments use the head face's normal (`originalNormals[n-1]`). Per-section normals cause perpendicular quads when a face boundary slides through the snake — adjacent samples get `Cross(up, dir)` vs `Cross(right, dir)` as right vectors, which are 90° apart.

**Speed constants:**
- `EJECT_STEP_TIME = 0.05f` → `speed = gridStep / 0.05` (units/s)
- `EJECT_LAUNCH_ACCEL = 80.0f` → finale accelerates at 80 units/s²
- `EJECT_FINAL_DURATION = 0.40f`

### SamplePathBufferAll

Back-traces from buffer tail to head, placing M evenly-spaced samples at `gridStep / S` intervals. Results placed tail-first (`results[M-1-j]`). Clamps to `buffer[0]` if buffer is shorter than needed.

### BlockedShakeSequence

Uses a **fixed** buffer (original positions + one virtual head slot) — only the virtual head moves. This prevents the V-shape/stretch artifact that occurs when the real head position shifts.

Push phase: head slot moves forward along `headDir` by `pushDist = gridStep * 0.5`.  
Pull phase: head slot returns to original.  
Restore: `SetPath(originalPositions, ..., useWorldSpace: false)` — skips world-to-local conversion for a pixel-perfect reset.

Per-section normals **are** used in BlockedShakeSequence (unlike EjectSequence) because the path stays on the original faces — no face boundary slides through.

---

## 11. ArrowConstants — Tuning Reference

**File:** `Unity/Assets/Scripts/ArrowConstants.cs`

| Constant | Value | Role |
|---|---|---|
| `DEFAULT_LINE_WIDTH` | `0.08f` | Arrow body width |
| `DEFAULT_TIP_LEN_MULT` | `2.5f` | Tip length = lineWidth × this |
| `DEFAULT_TIP_WID_MULT` | `2.5f` | Tip half-width = lineWidth × this × 0.5 |
| `DEFAULT_SURFACE_OFFSET` | `0.005f` | Lift off surface to prevent z-fighting |
| `EPS` | `0.0001f` | Degenerate vector guard |
| `MIN_SEG_LEN` | `0.01f` | Minimum segment length to prevent zero quads |
| `STRAIGHT_DOT_THR` | `0.99f` | `dot(dirIn, dirOut)` above this → no bend arc |
| `FOLD_DOT_THR` | `0.99f` | `dot(nA, nB)` above this → shared normal (edge segment) |
| `BEND_ANGLE_STEP` | `π/8` | Arc subdivision step |
| `COLLIDER_NORMAL_EPS` | `0.02f` | Collider proximity offset |
| `MITER_LIFT_THR` | `0.05f` | Multi-face vertex lift dot threshold |
| `EJECT_STEP_TIME` | `0.05f` | Seconds per grid step during ejection snake |
| `SHAKE_STEP_TIME` | `0.08f` | Seconds per grid step during shake |
| `SHAKE_PUSH_DIST_MULT` | `0.50f` | Push distance = gridStep × this |
| `EJECT_LAUNCH_ACCEL` | `80.0f` | Finale launch acceleration (units/s²) |
| `EJECT_FINAL_DURATION` | `0.40f` | Duration of off-screen launch phase |
| `LAYER_ARROW` | `"Arrow"` | Layer name for idle arrows |
| `LAYER_EJECTING_ARROW` | `"EjectingArrow"` | Layer name during ejection |

---

## 12. Materials

### ArrowPulseMat

Used by idle arrows (set directly on Arrow prefab `MeshRenderer.sharedMaterial`).

- `_MinArrowAlpha = 0.08` — back faces are nearly invisible
- Transparency driven by `dot(normalWS, cubeToCamDir)`

### ArrowEjectMat

Used during ejection. Assigned to `Arrow.arrowEjectMaterial` in the Inspector.

- Duplicate of `ArrowPulseMat` with `_MinArrowAlpha = 1.0`
- `lerp(1, 1, t) = 1` always — fully opaque regardless of normal alignment
- Do NOT add a shader flag fallback. Two material assets is the correct Unity pattern.

`MaterialFixer.cs` auto-assigns `ArrowPulseMat` to the prefab's `MeshRenderer.sharedMaterial` if missing (Editor-only utility).

---

## 13. Known Pitfalls & Hard-Won Rules

### Coordinate space

- **Never** store world positions in `originalPositions`. The `VerifyLocalSpace` guard logs an error if any point exceeds 50 units from local origin.
- `lastValidDir` in `CreateBuildContext` must use `localPos`, not `positions` — `positions` may be world-space.

### Normal conversion during ejection

Converting local normals to world before storing them in the mesh causes a double-transform. The shader applies `TransformObjectToWorldNormal` → if normals were already world, they get rotated twice → wrong alpha. **Normals must stay local** throughout the ejection setup.

### `InwardDir` sign

`if (Dot(d, otherFaceNormal) > 0) d = -d` points **away** from `otherFaceNormal`. Inverting this (`dot > 0 ? d : -d`) reverses the fold — wings appear on the wrong side.

### `IsEdgeSegment` break

```csharp
foreach (var nA in facesA)
    foreach (var nB in facesB)
        if (Dot(nA, nB) > threshold) { shared.Add(nA); break; } // break is critical
```

Without `break`, one `nA` can match multiple `nB`s, over-counting shared normals → fold segments incorrectly detected on face-only segments.

### `BuildBends` fold guard includes Corner exception

```csharp
if (prevFold && nextFold && ctx.dotTypes[i] != DotType.Corner) continue;
```

Both adjacent fold segs → skip, **unless** the dot is a Corner. Corner dots (3 normals) always have a third face that fold quads never cover — `AddFoldedRoundCap` must run.

### `AddBendArc` winding

Fixed winding only works for one turn direction. Always branch on `angleTo` sign, not a constant.

### `AddFoldTip` wing lift

Wing vertices use `n1 * offset` lift only. Adding `edgeLift` on top double-lifts and floats wings above the surface.

### Collider guard during ejection

`Arrow.SetPath` must check `!isEjecting` before calling `UpdateSegmentColliders` / `UpdateTipCollider`. Without this, each frame of `EjectSequence` re-enables colliders that were just disabled.

### IsArrowBlocked layer mask

`LayerMask.GetMask(ArrowConstants.LAYER_ARROW)` — computed at runtime from the layer name. Do **not** use the Inspector `LayerMask arrowLayer` field for blocking queries; it defaults to 0 (nothing) if not set.

### `hit.normal` unreliability for BoxColliders

For a thin BoxCollider on a back-face arrow, the raycast hits the camera-facing underside of the box, giving `hit.normal` pointing toward camera — incorrectly passing any dot-product visibility check. Always use `ArrowSegmentFace.localFaceNormal` instead.
