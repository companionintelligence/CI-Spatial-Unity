# 3D World Mapping Standards — glTF Scene Format

This document describes the glTF scene format used by the Companion Scan
pipeline (Unity Quest runtime + Web client) for representing 3D world maps,
spatial pins, and semantic annotations.

---

## 1. Overview

The Companion Scan pipeline exports spatial data as **glTF 2.0** (`.gltf` /
`.glb`) files with proprietary metadata embedded in the root `extras` object.
The format is designed to be:

- **Self-contained** — one file carries both geometry and semantic annotation.
- **Tool-agnostic** — the `extras` field is optional; any standard glTF viewer
  can open the file and render the spatial geometry.
- **Round-trip safe** — the World Map Viewer/Editor can load and re-export the
  file without losing metadata.

---

## 2. File Structure

```
scan_<timestamp>.gltf
├── asset          { version: "2.0" }
├── scenes         [ { nodes: [...] } ]
├── nodes          [ ... spatial mesh nodes ... ]
├── meshes         [ ... ]
├── materials      [ ... ]
├── textures       [ ... ]
└── extras         <CI-XR extras — see §3>
```

The `extras` block is placed at the **root level** of the glTF JSON
(not inside a node or mesh), as allowed by the glTF 2.0 specification
(§3.7.2 Extras and Extensions).

---

## 3. Root-Level Extras Schema

```typescript
interface GltfExtras {
  /** Session identifier (UUID or timestamp string). */
  session_id: string;

  /** Unix epoch seconds when the file was exported. */
  export_timestamp: number;

  /** All spatial pins in the scene (see §4). */
  pins: SpatialPin[];

  /** Total number of pins. */
  pin_count: number;

  /** Pins produced by the CV Server pipeline. */
  cv_pin_count: number;

  /** Pins produced by Meta Scene Understanding (on-device). */
  meta_scene_pin_count: number;
}
```

---

## 4. Spatial Pin Schema

Each `SpatialPin` represents one annotated observation in world space.

### 4.1 Identity Fields

| Field          | Type     | Description                                         |
|----------------|----------|-----------------------------------------------------|
| `label`        | string   | Human-readable object label (e.g. `"chair"`).      |
| `semanticLabel`| string   | Normalised / canonical label (may equal `label`).   |
| `score`        | number   | Detection confidence in `[0, 1]`.                   |
| `source`       | string   | `"CvServer"` or `"MetaScene"`.                      |

### 4.2 Spatial Fields

All positions and vectors are in **world-space metres**, right-handed
coordinate system, Y-up (matches Unity's coordinate frame after
the glTF→Unity conversion).

| Field      | Type              | Description                               |
|------------|-------------------|-------------------------------------------|
| `position` | `[x, y, z]`       | World-space centroid in metres.           |
| `normal`   | `[x, y, z]`       | Surface normal at the pin site (unit vec).|
| `rotation` | `[x, y, z, w]`    | Orientation quaternion.                   |

### 4.3 2-D Bounding Box (CV Server only)

| Field           | Type         | Description                              |
|-----------------|--------------|------------------------------------------|
| `bboxPixels`    | BBoxNorm\|null | Detection bbox in pixel coordinates.   |
| `bboxNormalized`| BBoxNorm\|null | Detection bbox normalised to `[0, 1]`. |

```typescript
interface BBoxNorm {
  x: number;
  y: number;
  width: number;
  height: number;
}
```

### 4.4 Geometry (Meta Scene Understanding only)

| Field        | Type                   | Description                           |
|--------------|------------------------|---------------------------------------|
| `planeSize`  | `[w, h, d]` \| null    | Detected plane dimensions in metres.  |
| `volumeSize` | `[w, h, d]` \| null    | Detected volume dimensions in metres. |
| `anchorUuid` | string \| null         | XRAnchor UUID from the device.        |

### 4.5 Capture Metadata

| Field                | Type   | Description                                     |
|----------------------|--------|-------------------------------------------------|
| `sourcePrompt`       | string | Text prompt used for detection.                 |
| `timestamp`          | number | Unix seconds when the pin was created.          |
| `captureId`          | string | Identifier of the source capture frame.         |
| `captureSessionId`   | string | Session ID of the capture run.                  |
| `captureFrameIndex`  | number | Frame index within the capture session.         |
| `placementConfidence`| number | Confidence in 3-D placement `[0, 1]`.           |
| `hitType`            | string | How position was determined (see §4.6).         |
| `trackId`            | number | Deduplication track ID (0 = untracked).         |

### 4.6 Hit Types

| Value          | Description                                             |
|----------------|---------------------------------------------------------|
| `ArPlane`      | Raycast hit against an AR plane (WebXR hit-test).       |
| `Environment`  | Raycast hit against the passthrough environment mesh.   |
| `MetaPlane`    | Position derived from a Meta Scene Understanding plane. |
| `Physics`      | Raycast hit against a Unity physics collider.           |
| `Fallback`     | Camera-forward depth estimate when no surface detected. |

---

## 5. Pin Source Pipeline Comparison

| Aspect             | `CvServer` (CV pipeline)          | `MetaScene` (on-device)              |
|--------------------|-----------------------------------|--------------------------------------|
| **Trigger**        | User captures a frame             | Continuous background scan           |
| **Detection**      | Florence-2 → GDino → SAM2        | Meta Scene Understanding API         |
| **3-D placement**  | WebXR hit-test or fallback        | XRPlane / XRAnchor from device        |
| **Normal**         | Estimated from hit surface        | Plane normal from XR plane           |
| **Bbox**           | Normalised 2-D from model         | null                                 |
| **Track ID**       | Assigned by SpatialPinManager     | 0 (not tracked)                      |

---

## 6. Coordinate System

```
        Y (up)
        │
        │    Z (into screen / forward in Unity)
        │   ╱
        │  ╱
        │ ╱
        └──────────── X (right)
```

- All world-space vectors use **Unity's left-handed Y-up** coordinate system.
- The glTF standard uses **right-handed Y-up**; the Unity glTF importer
  (`UnityGLTF`) automatically applies the `[+X +Y -Z]` → `[+X +Y +Z]`
  mirror on import.
- When consuming the file outside Unity, treat the coordinate frame as
  standard glTF (right-handed Y-up) and negate Z if needed.

---

## 7. Deduplication and Smoothing

The `SpatialPinManager` runs a lightweight spatial-dedup pass:

1. For each new pin, find existing pins within **0.25 m** and with the same
   `label`.
2. If a match is found, exponentially smooth the position:
   `pos = lerp(existing.pos, new.pos, 0.3)`.
3. Increment the `trackId` counter; pins with the same `trackId` represent
   the same physical object observed multiple times.

Duplicate pins (score < threshold or spatial overlap) are discarded before
export.

---

## 8. World Map Viewer / Editor

The bundled `viewer.html` page provides a browser-based GLTF world map viewer
and editor inspired by the [Pascal Editor](https://github.com/pascalorg/editor)
architecture.

### Architecture (Pascal Editor pattern)

| Component           | Pascal Editor analogue    | Responsibility                                    |
|---------------------|---------------------------|---------------------------------------------------|
| `SceneStore`        | `useScene` (Zustand)      | Flat node dictionary, undo/redo, events           |
| `ViewerRenderer`    | `@pascal-app/viewer`      | Three.js renderer, OrbitControls, lighting, grid  |
| `PinRenderer`       | `NodeRenderer` + Systems  | Reactive pin mesh creation/update/deletion        |
| `GltfImporter`      | File I/O                  | Load `.gltf`/`.glb`, extract `extras`             |
| `WorldMapEditor`    | Editor orchestrator       | Composes all sub-systems, click-to-select, export |

### Usage

1. Open `viewer.html` in a browser (served by `npm run dev` or `npm run preview`).
2. Drag a `.gltf` or `.glb` world map file onto the canvas **or** click
   **Load GLTF**.
3. Orbit the scene with left-drag, pan with right-drag, zoom with scroll.
4. Click any pin sphere to select it; edit its `label`, `score`, and
   `confidence` in the inspector panel.
5. Click **Export GLTF** to download the edited scene.
6. Use **Undo** / **Redo** or `Ctrl+Z` / `Ctrl+Y` to navigate history.
7. Press `Delete` to remove the selected pin.

---

## 9. Unity ↔ Web Round-Trip

```
Unity Quest runtime
  └── SpatialMapExporter.cs     →  scan.gltf (with extras)
                                         │
         ┌───────────────────────────────┤
         │ CV Server  /v1/gltf           │ Browser (viewer.html)
         │ (stores / analyses)           │ (view / edit / re-export)
         │                               │
  Unity  └── UnityGLTF importer  ←  edited_scan.gltf
```

The `extras` block is preserved through all stages. Any field that the
current pipeline does not understand is kept as-is in the output file,
ensuring forward compatibility.

---

## 10. Versioning

The extras schema does not yet carry an explicit version field. Future
breaking changes should add a `schema_version` field at the root of the
extras object:

```json
{
  "schema_version": "1.0.0",
  "session_id": "...",
  ...
}
```

Parsers should treat a missing `schema_version` as `"0.x"` (current legacy
format).
