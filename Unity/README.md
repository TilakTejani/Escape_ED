# ESCAPE-ƎD (Unity)

A 3D surface puzzle game built in Unity (URP). Arrows are drawn on the faces, edges, and corners of a rotating cube. Players tap arrows to eject them — but only if their path is clear.

---

## Scene Setup

Open `Unity/Assets/Scenes/SampleScene.unity`.

The scene has one root GameObject that carries all core components:

| Component | Role |
|---|---|
| `CubeGrid` | Grid data, dot types, world position math |
| `LevelManager` | Level loading, arrow spawning, block detection |
| `CubeRotator` | Drag-to-rotate and pinch-to-zoom |
| `GhostCubeController` | Per-face transparency based on camera angle |
| `InteractionSystem` | Tap → raycast → IInteractable |

Arrows are spawned as children of this root object at runtime.

---

## Getting Started

1. Open project in **Unity 2022.3+** (URP).
2. Select the root GameObject in the Hierarchy.
3. Assign a JSON level file to `LevelManager → levelJsonFile`.
4. Press Play. The level loads automatically on `Start`.

To test without a JSON file, right-click `LevelManager` in the Inspector and choose **Generate Procedural Level**.

---

## Project Structure

```
Unity/Assets/
  Scenes/
    SampleScene.unity
  Scripts/
    Arrow.cs                  — Main arrow component (IInteractable)
    ArrowMeshBuilder.cs       — Static mesh generation (body, tip, caps, bends)
    ArrowAnimator.cs          — Eject and blocked-shake coroutines
    ArrowPhysicsHandler.cs    — BoxCollider pool + convex tip MeshCollider
    ArrowConstants.cs         — All magic numbers and layer names
    CubeGrid.cs               — Grid data, DotType, world position math
    LevelManager.cs           — Level loading, arrow spawning, blocking logic
    GhostCubeController.cs    — Per-face alpha based on camera orientation
    LevelSchema.cs            — JSON data structures + LevelLoader utility
    CubeRotator.cs            — Touch/mouse rotation and zoom
    CubeNavigator.cs          — Grid navigation helpers
    GameStateManager.cs       — Global game state (Playing, Paused, etc.)
    Input/
      InputController.cs      — New Input System wrapper
      InputReader.cs          — Raw input reading
      InteractionSystem.cs    — Tap → raycast → IInteractable bridge
      IInteractable.cs        — Interface for tappable objects
      TouchData.cs            — Touch data struct
  Shaders/
    ArrowPulsing.shader       — URP shader: face-fade + pulse animation
```

---

## Level Format

Levels are defined in JSON and assigned to `LevelManager.levelJsonFile`.

See **[LEVEL_FORMAT.md](LEVEL_FORMAT.md)** for the full schema and examples.

---

## Key Inspector Fields

### LevelManager
| Field | Description |
|---|---|
| `levelJsonFile` | JSON TextAsset defining the puzzle |
| `arrowPrefab` | Prefab with Arrow component |
| `arrowMaterial` | Material for on-cube arrows (face-fade shader) |
| `grid` | Reference to CubeGrid component |

### CubeGrid
| Field | Description |
|---|---|
| `size` | Grid dimensions (e.g., 5×5×5) |
| `autoScale` | Derives spacing/dotRadius from arrow width automatically |
| `spacingMult` | Spacing = arrowWidth × spacingMult |
| `whiteMaterial` | Material for cube face quads |

### Arrow
| Field | Description |
|---|---|
| `arrowMaterial` | Standard on-cube material |
| `arrowEjectMaterial` | Material applied during ejection (ZTest Always) |
| `lineWidth` | Arrow body width in world units |
| `tipLengthMult` | Arrowhead length = lineWidth × tipLengthMult |
| `tipWidthMult` | Arrowhead base width = lineWidth × tipWidthMult |
| `surfaceOffset` | Lift distance from cube surface |
| `smoothShading` | Toggle between flat normals and RecalculateNormals |

---

## Platforms

- **iOS / Android** — primary target, touch input, optimized for low draw calls
- **Editor / Desktop** — mouse drag rotation, keyboard shortcut K to eject all arrows

---

## Architecture

See **[DESIGN.md](DESIGN.md)** for the full technical design: coordinate spaces, mesh pipeline, animation system, and blocking detection.
