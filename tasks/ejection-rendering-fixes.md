# Arrow Ejection & Rendering Fixes

## Problems Addressed

1. Ejecting arrows going transparent mid-flight
2. Arrow mesh geometry wrong during ejection on rotated cubes
3. Perpendicular segments during snake ejection animation on multi-face arrows
4. Tapping invisible (back-facing) arrow segments triggering interaction

---

## Fix 1 — ArrowEjectMat `_MinArrowAlpha` (Inspector)

**File:** `Unity/Assets/ArrowEjectMat` (Material asset)

**Problem:**  
`ArrowEjectMat._MinArrowAlpha` was `0.08`. The shader alpha formula:
```glsl
alpha = lerp(_MinArrowAlpha, 1.0, t)
```
still ran during ejection, making arrows fade based on (wrong) normals.

**Fix:**  
Set `_MinArrowAlpha = 1.0` in the Inspector.  
`lerp(1, 1, t) = 1` always — fully opaque regardless of normal direction.

---

## Fix 2 — Ejection Normal Double-Transform (`ArrowAnimator.cs`)

**File:** `Unity/Assets/Scripts/ArrowAnimator.cs` — `EjectSequence()`

**Problem:**  
Normals were converted from local → world space before ejection:
```csharp
worldInner.Add(_owner.transform.TransformDirection(norm)); // local → world
```
Then written into the mesh as object-space normals. The shader then applied
`TransformObjectToWorldNormal` again → **double rotation** → wrong world normal →
wrong alpha computation.

**Fix:**  
Removed the `worldNormals` conversion block entirely. Normals stay in **local space**.  
After `SetParent(null, worldPositionStays:true)`, the arrow's transform retains the
cube's rotation. `TransformObjectToWorldNormal` in the shader handles the single
correct conversion.

```csharp
// BEFORE — caused double-transform
var worldNormals = new List<List<Vector3>>(n);
foreach (var list in originalNormals) {
    var worldInner = new List<Vector3>(list.Count);
    foreach (var norm in list)
        worldInner.Add(_owner.transform.TransformDirection(norm));
    worldNormals.Add(worldInner);
}
var (primaryNormalLists, _) = BuildPrimaryNormalLists(worldNormals, n);

// AFTER — normals stay local
var (primaryNormalLists, _) = BuildPrimaryNormalLists(originalNormals, n);
```

---

## Fix 3 — Perpendicular Segments During Ejection Snake (`ArrowAnimator.cs`)

**File:** `Unity/Assets/Scripts/ArrowAnimator.cs` — `EjectSequence()`

**Problem:**  
The snake animation assigned normals per-section:
- Sample `j` → `originalNormals[j/S]`
- Tail: `originalNormals[0]`, Head: `originalNormals[n-1]`

As positions slid forward each frame, a sample physically on face B still had
face A's normal. At face transitions, adjacent samples got:
```
Cross(up,    dir) = X  →  quad spreads horizontal
Cross(right, dir) = Y  →  quad spreads vertical
```
Adjacent quads **90° apart** — visually perpendicular segments.

**Fix:**  
Use the **exit face normal** for all snake segments:
```csharp
var exitNormal = new List<Vector3> {
    originalNormals[n - 1].Count > 0 ? originalNormals[n - 1][0] : Vector3.up
};
for (int j = 0; j < M; j++) {
    activeNormals.Add(exitNormal);
    activeDotTypes.Add(DotType.Face);
}
```
One consistent normal means every segment's `Cross(faceN, dir)` produces the
same right vector → ribbon is flat and uniform throughout animation.

---

## Fix 4 — Back-Face Arrow Interaction Guard (`InteractionSystem.cs`, `ArrowPhysicsHandler.cs`)

### Part A — `ArrowSegmentFace` component (`ArrowPhysicsHandler.cs`)

**Problem:**  
`hit.normal` from `Physics.Raycast` is the normal of the **BoxCollider face that
was hit** — not the arrow's face normal. A back-face arrow's BoxCollider has its
camera-side underside facing the ray, so `hit.normal` points toward the camera
even though the arrow is invisible.

**Fix:**  
Added `ArrowSegmentFace` MonoBehaviour to each segment collider child:
```csharp
public class ArrowSegmentFace : MonoBehaviour {
    public Vector3 localFaceNormal;
}
```
Stamped on creation and updated every `UpdateSegmentColliders` call:
```csharp
col.GetComponent<ArrowSegmentFace>().localFaceNormal = faceN;
```

### Part B — Raycast face-normal check (`InteractionSystem.cs`)

**Fix:**  
After raycast hit, read `ArrowSegmentFace.localFaceNormal` (local space),
transform to world via the arrow's transform (parent of collider child),
and reject if it faces away from camera:
```csharp
var segFace = hit.collider.GetComponent<ArrowSegmentFace>();
Vector3 faceNormalWS = segFace != null
    ? hit.collider.transform.parent.TransformDirection(segFace.localFaceNormal)
    : hit.normal; // tip MeshCollider — mesh normals are correct

if (Vector3.Dot(faceNormalWS, -ray.direction) <= 0f) return;
```

The tip `MeshCollider` has no `ArrowSegmentFace` — falls back to `hit.normal`
which is correct for convex mesh triangles.

---

## Fix 5 — Ejection Speed (`ArrowConstants.cs`)

**File:** `Unity/Assets/Scripts/ArrowConstants.cs`

Ejection speed increased to **2x**:

| Constant | Before | After | Role |
|---|---|---|---|
| `EJECT_STEP_TIME` | `0.10f` | `0.05f` | Seconds per grid step — lower = faster snake |
| `EJECT_LAUNCH_ACCEL` | `40.0f` | `80.0f` | Finale launch acceleration |

`EJECT_FINAL_DURATION` unchanged at `0.40f`.

---

## Coordinate Space Contract (Reference)

The arrow system uses a strict local-space contract:

- `originalPositions` — stored in **local space** (cube's transform)
- `SetPath()` receives **world** positions → `InverseTransformPoint` → local
- `GetEjectionData()` and animators call `TransformPoint(originalPositions[i])` to get world
- Face normals from `CubeGrid.GetAllFaceNormals` — **local space** axis vectors (`Vector3.up` etc.)
- Shader `TransformObjectToWorldNormal` handles local → world for rendering

After ejection detach (`SetParent(null, worldPositionStays:true)`):
- Arrow transform retains cube's world rotation
- Local normals remain valid — shader transform is still correct
- World positions in `pathBuffer` are converted to local each frame via `InverseTransformPoint`
