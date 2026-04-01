# ESCAPE-ƎD (Unity Engine)

**ESCAPE-ƎD** is a 3D isometric puzzle game built in Unity. The core gameplay revolves around navigating a cube grid using procedural arrows that wrap around its surface.

## 🚀 Key Features

- **High-Fidelity Rendering**: Custom mesh folding logic that allows arrows to seamlessly wrap around cube edges and corners.
- **Dynamic Level Generation**: 1:1 parity with the [Level Maker](file:///Users/t.d.tejani/Projects/Escape_ED/cube-arrow-maker) for deterministic puzzle loading.
- **Optimized UI Shaders**: Neon pulse effects with zero Z-fighting, ensuring a premium mobile-ready look.

---

## 🛠️ Architecture Overview

The Unity project is built with a focus on **procedural geometry**:

1. **`CubeGrid.cs`**: Generates a surface-embedded grid of dots.
2. **`Arrow.cs`**: Dynamically builds arrow meshes that follow arbitrary paths on the cube.
3. **`LevelManager.cs`**: Orchestrates the loading of JSON levels and spawning of visual elements.

For a deep dive into the technical details and algorithms used (like the "Folding" logic), please see the **[DESIGN.md](file:///Users/t.d.tejani/Projects/Escape_ED/Unity/DESIGN.md)**.

---

## 🏗️ Getting Started

1. Open the project in Unity 2022.3+ (URP).
2. Load the **SampleScene** in `Unity/Assets/Scenes/`.
3. Press **Force Regenerate Grid** in the `CubeGrid` Inspector to refresh visuals.
4. Use the **ContextMenu** on `LevelManager` to "Generate Test Level."

---

## 📱 Platforms
- **iOS/Android**: Optimized for touch and low draw calls.
- **Web/Desktop**: Full mouse support.
