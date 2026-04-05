# Escape-ƎD Architecture

## System Overview

The project is a Unity puzzle game where players tap arrows on the surface of a 3D cube to eject them. The codebase is split across focused, single-responsibility files.

```
GameStateManager          ← Singleton FSM, broadcasts state changes
       ↓
LevelManager              ← Orchestrator: loads JSON, spawns arrows, handles blocking/ejection
       ↓
CubeGrid + CubeNavigator  ← Grid topology, world↔local coords, path navigation
       ↓
Arrow                     ← Entity: delegates to 3 subsystems
  ├─ ArrowMeshBuilder      ← Static pipeline: Body → TailCap → Bends → Tip → FinalizeMesh
  ├─ ArrowPhysicsHandler   ← Pooled BoxColliders + dynamic tip MeshCollider
  └─ ArrowAnimator         ← Coroutines: EjectSequence, BlockedShakeSequence
       ↓
Input Pipeline
  InputReader → InputController → InteractionSystem → IInteractable.OnInteract()
       ↓
Camera
  CubeRotator (drag / inertia / pinch zoom)
  GhostCubeController (Fresnel face transparency)
```

---

## File Responsibilities

| File | Role |
|---|---|
| `GameStateManager.cs` | Singleton FSM — Init, MainMenu, Playing, Solving, Success, Failure |
| `LevelManager.cs` | Loads JSON levels, spawns arrows, validates blocking, ejects arrows |
| `CubeGrid.cs` | Surface topology, dot indexing, procedural visual mesh, auto-scaling |
| `CubeNavigator.cs` | Grid-based path navigation with surface-normal-aware direction basis |
| `Arrow.cs` | MonoBehaviour orchestrator, state flags, SetPath pipeline entry point |
| `ArrowMeshBuilder.cs` | Static procedural mesh generator — body, caps, bends, tip |
| `ArrowPhysicsHandler.cs` | Pooled BoxColliders per segment + convex MeshCollider for tip |
| `ArrowAnimator.cs` | Eject and blocked-shake coroutines, path buffer interpolation |
| `ArrowConstants.cs` | All magic numbers — dimensions, thresholds, timings, layer names |
| `CubeRotator.cs` | Camera orbit with inertia, pinch/scroll zoom, dynamic zoom limits |
| `GhostCubeController.cs` | Fresnel-based face transparency, arrow renderer alpha management |
| `InputReader.cs` | Hardware abstraction — unifies touchscreen + mouse into TouchData events |
| `InputController.cs` | Gesture detection — tap duration/distance thresholds, per-finger state |
| `InteractionSystem.cs` | Raycasts screen taps to IInteractable in 3D world, UI pointer guard |
| `LevelSchema.cs` | Serializable data classes + JSON parser for level files |
| `IInteractable.cs` | Interface: `OnInteract()` |
| `TouchData.cs` | Struct: fingerId, position, phase, time |

---

## Player Interaction Flow

```
Tap arrow
  └─ InteractionSystem raycast
     └─ Arrow.OnInteract()  (IInteractable)
        └─ LevelManager.HandleArrowInteraction()
           ├─ Blocked?  → arrow.PlayBlockedAnimation()
           │              ArrowAnimator: push forward → pull back → restore
           └─ Free?     → arrow.Eject()
                          ArrowAnimator: detach from cube → animate along heading → Destroy
```

---

## Key Design Decisions

### Composition over Inheritance (Arrow)
`Arrow` is a thin orchestrator. All domain logic is delegated:
- `ArrowMeshBuilder` owns rendering
- `ArrowPhysicsHandler` owns collision
- `ArrowAnimator` owns animation

This keeps `Arrow.cs` readable and allows each handler to evolve independently.

### Local Space Contract (Arrow)
`originalPositions` is stored in **local space** (Arrow transform = cube transform).

- `SetPath()` receives world positions
- `CreateBuildContext()` converts to local via `InverseTransformPoint`
- Snapshot is taken after conversion — so `originalPositions` is always local

This prevents "ghost paths" when the cube rotates during ejection. `GetEjectionData()` and both animators call `TransformPoint(originalPositions[i])` to recover world positions — this only works if the snapshot is local.

### Zero-GC Hot Paths
| Technique | Where |
|---|---|
| Pre-allocated `Collider[64]` array | `LevelManager.overlapResults` |
| `OverlapCapsuleNonAlloc` | `LevelManager.IsArrowBlocked` |
| Pooled `BoxCollider` children | `ArrowPhysicsHandler._segmentColliders` |
| Reusable `List<>` buffers (`_verts`, `_tris`, etc.) | `Arrow` |
| `MaterialPropertyBlock` (no material instantiation) | `GhostCubeController` |

### Input Abstraction Layers
```
InputReader        ← hardware (touchscreen + mouse → TouchData)
InputController    ← gestures (tap vs. drag, per-finger state)
InteractionSystem  ← world bridge (raycast + UI guard)
```
Each layer is independently testable and replaceable.

### Static Mesh Builder
`ArrowMeshBuilder` is a static utility class with a `Context` struct that carries all state. This avoids MonoBehaviour overhead and keeps the mesh pipeline functional — each stage (`BuildBody`, `BuildTailCap`, `BuildBends`, `BuildTip`) is a pure transformation on the context.

---

## Performance Notes

- `sharedMesh` is used everywhere (not `.mesh`) to avoid implicit copies
- `MarkDynamic()` is called on both the arrow mesh and the tip collider mesh
- `mesh.Clear()` + full rebuild runs every `SetPath()` call. During ejection this is once per frame. Acceptable at puzzle-game scale (~10 arrows).
- Arrows are `Instantiate`/`Destroy`'d — no pooling at the GameObject level. Fine for current scale.

---

## Scalability: Extending to Non-Cube Shapes

### What is already shape-agnostic

The entire bottom layer does not know it is on a cube:

| System | Why |
|---|---|
| `ArrowMeshBuilder` | Only consumes `positions`, `allNormals`, `dotTypes` |
| `ArrowPhysicsHandler` | Works on any path |
| `ArrowAnimator` | Works on any path |
| `InputReader/Controller/InteractionSystem` | Fully generic |
| `CubeRotator` | Generic orbit camera |

**If you can supply `positions + allNormals + dotTypes` for any surface, `ArrowMeshBuilder` produces correct geometry without modification.**

### What is cube-specific (must change for new shapes)

**`CubeGrid`** — entire class assumes axis-aligned box topology:
- `IsSurface(x, y, z)` checks axis boundaries
- `GetSurfaceNormal()` returns one of 6 axis-aligned vectors
- `GetAllFaceNormals()` based on `x == 0`, `x == size.x-1`, etc.
- `GetDotType()` counts how many of 3 axes are on a boundary
- `CalculateWorldPos()` linear x,y,z offset grid

**`CubeNavigator`** — uses `Vector3Int` grid math. Arbitrary shapes require graph-based adjacency.

**`GhostCubeController`** — hardcoded 6-face normal array. Needs to be N-face generic.

**`LevelSchema.GridSize`** — assumes rectangular grid addressing.

### Recommended abstraction path

Extract an interface from `CubeGrid`'s existing public API (signatures are already shape-agnostic):

```csharp
public interface ISurfaceShape
{
    int PointCount { get; }
    Vector3          GetWorldPosByIndex(int index);
    List<Vector3>    GetAllFaceNormals(int index);
    DotType          GetDotType(int index);
    List<int>        GetAdjacentPoints(int index);  // new — drives navigation
}
```

`LevelManager` only calls these methods — it would work against the interface unchanged.

`CubeNavigator` becomes a graph traversal over `GetAdjacentPoints`, guided by camera-space direction to pick the "most up/down/left/right" neighbor.

`GhostCubeController` needs to accept a variable face list rather than a hardcoded 6-element array.

### Effort by shape

| Shape | Effort | Notes |
|---|---|---|
| Non-square box (e.g. 3×4×5) | None | Already works — `CubeGrid.size` supports non-uniform |
| Cylinder | Medium | Regular topology, seam handling at wrap |
| Pyramid / Prism | Medium-Hard | Triangular faces, DotType counts still valid |
| Sphere (icosphere) | Hard | Irregular adjacency (~5–6 neighbors), direction is ambiguous |
| Arbitrary polyhedron | Very Hard | Requires mesh topology graph, custom navigation |

### DotType on non-cube shapes

`DotType.Face / Edge / Corner` maps naturally to any polyhedron — it just counts how many faces meet at a vertex (1 = Face, 2 = Edge, 3+ = Corner). The enum and all of `ArrowMeshBuilder`'s geometry logic remains valid regardless of shape.

---

## What Is Stubbed / Not Yet Implemented

- `GameStateManager` state handlers are empty — no win condition wiring
- No level progression / unlock system
- No save / load
- No audio or VFX hooks
- No UI transitions between states

The natural next build frontier is wiring `GameStateManager` to a win condition (all arrows ejected) and hooking that to UI.
