# Claude Code Workflow

## Workflow Orchestration

### 1. Plan Node Default
- Enter plan mode for ANY non-trivial task (3+ steps or architectural decisions)
- If something goes sideways, STOP and re-plan immediately — don't keep pushing
- Use plan mode for verification steps, not just building
- Write detailed specs upfront to reduce ambiguity

### 2. Subagent Strategy
- Use subagents liberally to keep main context window clean
- Offload research, exploration, and parallel analysis to subagents
- For complex problems, throw more compute at it via subagents
- One task per subagent for focused execution

### 3. Self-Improvement Loop
- After ANY correction from the user: update `tasks/lessons.md` with the pattern
- Write rules for yourself that prevent the same mistake
- Ruthlessly iterate on these lessons until mistake rate drops
- Review lessons at session start for relevant project

### 4. Verification Before Done
- Never mark a task complete without proving it works
- Diff behavior between main and your changes when relevant
- Ask yourself: "Would a staff engineer approve this?"
- Run tests, check logs, demonstrate correctness

### 5. Demand Elegance (Balanced)
- For non-trivial changes: pause and ask "is there a more elegant way?"
- If a fix feels hacky: "Knowing everything I know now, implement the elegant solution"
- Skip this for simple, obvious fixes — don't over-engineer
- Challenge your own work before presenting it

### 6. Autonomous Bug Fixing
- When given a bug report: just fix it. Don't ask for hand-holding
- Point at logs, errors, failing tests — then resolve them
- Zero context switching required from the user
- Go fix failing CI tests without being told how

---

## Task Management

1. **Plan First:** Write plan to `tasks/todo.md` with checkable items
2. **Verify Plan:** Check in before starting implementation
3. **Track Progress:** Mark items complete as you go
4. **Explain Changes:** High-level summary at each step
5. **Document Results:** Add review section to `tasks/todo.md`
6. **Capture Lessons:** Update `tasks/lessons.md` after corrections

---

## Core Principles

- **Simplicity First:** Make every change as simple as possible. Impact minimal code.
- **No Laziness:** Find root causes. No temporary fixes. Senior developer standards.
- **Minimal Impact:** Changes should only touch what's necessary. Avoid introducing bugs.

---

## Project-Specific: Escape-ƎD Arrow System

### Architecture (post-modularization)

The arrow system is split across four files. Read all four before touching any:

| File | Role |
|---|---|
| `Arrow.cs` | MonoBehaviour orchestrator, state machine |
| `ArrowMeshBuilder.cs` | Static mesh geometry — body, tip, caps, bends |
| `ArrowAnimator.cs` | Eject + blocked-shake coroutines, path buffer |
| `ArrowPhysicsHandler.cs` | BoxCollider pool + convex tip MeshCollider |

### Coordinate Space Contract — CRITICAL

`originalPositions` is stored in **LOCAL space** (Arrow's transform = cube's transform).

- `SetPath()` receives **world** positions
- `CreateBuildContext()` converts to local via `InverseTransformPoint` → `ctx.localPos`
- Snapshot happens AFTER `CreateBuildContext`: `originalPositions = ctx.localPos`
- `GetEjectionData()` and both animators call `TransformPoint(originalPositions[i])` to get world positions — this only works if `originalPositions` is local

Violation: storing world positions in `originalPositions` causes double-transform after any cube rotation, producing wrong pathBuffer positions and a degenerate/invisible mesh during ejection.

### Modularization Pitfalls (learned the hard way)

When splitting a monolithic file into modules, these functions were subtly broken and must match the original exactly:

**`InwardDir`** — logic is easy to invert. Original:
```csharp
Vector3 d = Vector3.Cross(faceNormal, edgeDir).normalized;
if (Vector3.Dot(d, otherFaceNormal) > 0) d = -d;  // points AWAY from other face
return d;
```
Wrong version returns `dot > 0 ? side : -side` which points TOWARD other face → fold reversal.

**`IsEdgeSegment`** — must have `break` after matching each normal in facesA:
```csharp
foreach (var nA in facesA)
    foreach (var nB in facesB)
        if (Vector3.Dot(nA, nB) > threshold) { shared.Add(nA); break; }  // break is critical
```
Without `break`, one nA can match multiple nBs, over-counting shared normals.

**`AddFoldedRoundCap`** — the simple version (arc per face, 180°/270°) loses seam precision. Must use the full implementation: per-face 2D basis, InsertAngle/Atan2Basis for exact seam angles, miter-corrected lift at seam vertices.

**`AddFoldTip`** — wing vertices must use `n1 * offset` lift only (face-level lift). Adding `edgeLift` on top causes double-lift and floats wings above the surface.

### Blocking Detection

`IsArrowBlocked` uses `LayerMask.GetMask(ArrowConstants.LAYER_ARROW)` — computed at runtime from the layer name. Do NOT use the inspector `LayerMask arrowLayer` field for this query; it defaults to 0 (nothing) if not set in the Inspector.

### Collider Guard

`UpdateSegmentColliders` and `UpdateTipCollider` must NOT run during ejection. The guard is in `Arrow.SetPath`:
```csharp
if (!isEjecting)
{
    _physics.UpdateSegmentColliders(...);
    _physics.UpdateTipCollider(...);
}
```
Without this, `EjectSequence` calling `SetPath` each frame re-enables colliders that were just disabled.

### Debug Debt

`LevelManager.IsArrowBlocked` previously accumulated debug code (`FindObjectsOfType<BoxCollider>`, `OverlapSphere`, `Physics.SyncTransforms`, multiple `Debug.Log`). These have been removed. Do not re-add them.

### World Position from Grid

`CubeGrid.GetWorldPosByIndex` returns `transform.TransformPoint(CalculateWorldPos(...))` — true world space. `CalculateWorldPos` alone returns cube-local space. Always use `GetWorldPosByIndex` when world positions are needed.
