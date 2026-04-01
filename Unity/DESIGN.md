# ESCAPE-ƎD: Unity Engine Architecture

## 🎯 Design Goals
The Unity engine for ESCAPE-ƎD is designed to provide **geometric parity** with the [Level Maker](file:///Users/t.d.tejani/Projects/Escape_ED/cube-arrow-maker) while achieving a "premium" visual fidelity that simple primitives cannot provide.

---

## 🏗️ Core Systems

### 1. Procedural Grid System (`CubeGrid.cs`)
The grid is the "logic layer" that translates a 3D Vector3Int coordinate system into a surface-embedded visual layout.
- **Dynamic Mesh Building**: Instead of placing 100+ GameObjects (which increases draw calls), we generate custom circle meshes that "fold" over cube edges.
- **Normal Calculation**: Each grid point stores a list of face normals (1 for face, 2 for edges, 3 for corners). This is the foundation for the arrow folding logic.

### 2. Path & Arrow Generation (`Arrow.cs`)
The arrows are the interactive centerpiece of the puzzle.
- **The Folding Algorithm**: 
  - To prevent arrows from "clipping" through the cube at corners, we use a custom quad-splitting algorithm.
  - When a segment crosses an edge, `AddFoldQuads` bisects the arrow path, creating two perfectly joined quads on adjacent faces.
- **Apex Alignment**: Arrowheads are dynamically lifted based on the average normal of the destination dot, ensuring they stay flush regardless of orientation.

### 3. Visual & Shading (`ArrowPulsing.shader`)
We use a custom URP Shader to achieve a neon high-performance aesthetic.
- **Decal Simulation**: Using `ZWrite Off` and a `Depth Offset`, we simulate a sticker/decal effect. This completely removes "Z-fighting" artifacts between the arrow and the cube.
- **Flow Animation**: The shader animates a pulse color based on the horizontal UV distance, creating a sense of energy movement along the path.

---

## 🛠️ Developer Workflow

### Level Parsing
Levels use the `LevelSchema.cs` to deserialize JSON definitions.
1. `LevelManager` triggers `LevelLoader`.
2. `CubeGrid` initializes to the requested size.
3. Arrows are instantiated and their paths are calculated in **Task Space**.

### Key Prefabs
- **Arrow Container**: The root for the procedural mesh renderer.
- **Dot Material**: A deep, matte-black material for the grid background.

---

## 📱 Mobile Considerations
- **Draw Call Batching**: All dots and background elements are batched into a single static context (if possible).
- **Input System**: (Pending migration) Planned transition to the new `UnityEngine.InputSystem` for unified Touch/Mouse delta rotation.
