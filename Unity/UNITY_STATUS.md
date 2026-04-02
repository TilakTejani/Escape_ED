# ESCAPE-ƎD Unity Project Status

## Project Overview
A native Unity 3D puzzle game based on the React "Cube Arrow Maker." The project focuses on high-fidelity rendering, deterministic path logic, and a premium mobile-first interaction model.

## Current Implementation State

### 1. Grid Generation (`CubeGrid.cs`)
- **Deterministic Coordinate System:** Matches the React level maker's indexing (Z → Y → X).
- **Surface Detection:** Correctly identifies face, edge, and corner dots for pathing.
- **Dynamic Scaling:** Spacing and dot proportions are automatically calculated relative to the arrow line width.
- **Background System:** Generates 6 individual Quad faces to support per-face transparency.

### 2. Arrow Dynamics (`Arrow.cs`)
- **Robust Transformations:** World-to-local conversion ensures arrows remain pinned to the cube even when moved.
- **Refined Arrowheads:** Automatic scaling and segment trimming to prevent visual artifacts on short or bent segments.
- **Directional Logic:** Full support for paths that wrap around cube edges and corners.

### 3. "Ghost Cube" Visuals (`GhostCubeController.cs`)
- **Orientation-Based Transparency:** Uses Fresnel-style logic to fade out faces pointing at the camera.
- **Configuration:** Adjustable Min/Max Alpha and Fresnel Power settings on the GameManager.
- **Performance:** Optimized using Material Property Blocks to avoid material duplication.

### 4. Input & Control (`CubeRotator.cs`)
- **Smooth Rotation:** Physics-less touch/mouse rotation with adjustable smoothing and friction.
- **Zoom Support:** Integrated camera distance control.

## Known Issues & Pending Work

### 1. Transparency Synchronization
- **Issue:** The "Ghost" transparency effect currently only runs in **Play Mode**.
- **Requirement:** Add `[ExecuteAlways]` and Scene View camera support to make the effect visible during design-time in the Unity Editor.

### 2. Gameplay Mechanics
- **Collision/Blocker System:** Arrows currently do not detect each other. Logic is required to prevent arrows from passing through occupied grid paths.
- **Victory Conditions:** Full level-cleared state detection.
- **Level Loading:** Parsing JSON strings directly into the grid state.

---
*Last Updated: 2026-04-02*
