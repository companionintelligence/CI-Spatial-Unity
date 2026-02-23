# Companion Scan

Quest 3 local world/space mapper that combines Meta scene understanding with a local CV server (Grounding DINO, SAM 2, Florence-2) to produce a glTF export with rich spatial metadata and sync it back to the server.

![florence-xr-Trim-Trim-ezgif com-optimize](https://github.com/user-attachments/assets/429c9837-574e-4857-8843-1727167f73c3)

## Overview

Two independent capture systems feed a **unified spatial pin store**:

- **Meta Scene Understanding** (Pipeline A) — on-device OVRAnchor queries for semantic labels, planes, volumes. No network needed. See [Meta Scene API docs](https://developers.meta.com/horizon/documentation/unity/unity-scene-overview/).
- **CV Server Pipeline** (Pipeline B) — passthrough camera frames → Florence2 → GroundingDINO → SAM2 via a local Docker server. Raycasts detections into world space.

Both pipelines write `SpatialPin` objects (tagged `CvServer` or `MetaScene`) to the shared `SpatialPinManager`. The glTF export merges data from both sources.

## Key Paths

- Scene: `Assets/CompanionScanLegacy/Scenes/CompanionScanLegacy.unity`
- Companion Scan runtime: `Assets/CompanionScan/`
- Controller UI: `Assets/CompanionScan/UI/`
- CV server: `cv-server/`

## Architecture

```text
Quest 3 App
  ├─ Pipeline A: Meta Scene Understanding (on-device)
  │    OVRAnchor → OVRRoomLayout → OVRSemanticLabels
  │    → MetaSceneSnapshot → SpatialPin (MetaScene)
  │
  ├─ Pipeline B: CV Server (network)
  │    Camera JPEG → Florence2 caption + OD
  │    → GroundingDINO detect → SAM2 segment
  │    → Raycast → SpatialPin (CvServer)
  │
  ├─ SpatialPinManager (unified pin store)
  │
  ├─ XR Controller Input (button → action dispatch)
  │
  └─ glTF export + sync → /v1/gltf

Local CV Server (Docker)
  - /v1/detect   (GroundingDINO)
  - /v1/segment  (Grounded SAM 2)
  - /v1/florence  (Florence-2)
  - /v1/gltf     (upload)
```

## Controller Button Mapping

All interaction uses the physical Quest controller buttons — no world-space
UI panels. Each button has a coloured circle indicator and label attached
to the controller model at runtime.

### Right Controller

| Button | Colour | Action | Details |
|--------|--------|--------|---------|
| **A** | Red | **Capture** | Takes a snapshot. Runs both Meta Scene + CV Server pipelines (respects current model preset). 3D labels persist after each capture. |
| **B** | Blue | **Cycle Model** | Cycles the active AI model preset: Florence → GDino → SAM2 → All → Florence … |

### Left Controller

| Button | Colour | Action | Details |
|--------|--------|--------|---------|
| **X** | Yellow | **Preview Distance** | Cycles the raycaster fallback distance: Near (1 m) → Far (3 m) → Off → Near … |
| **Y** | Green | **Save & Export** | Exports glTF and uploads to the CV server via `GltfSyncClient`. |

### Either Controller

| Button | Colour | Action | Details |
|--------|--------|--------|---------|
| **Index Trigger** | Orange | **Display Mode** | Cycles visualisation: BoundingBox2D → SpatialLabel3D → Both → BoundingBox2D … |

### Wrist HUD

A small world-space panel is attached to the left controller wrist showing
the current state:

```
Model: All
Display: Both
Preview: Near (1m)
BUSY…            ← shown during capture
```

### Model Presets

| Preset | Florence | GDino | SAM2 |
|--------|----------|-------|------|
| Florence | ✓ | – | – |
| GDino | – | ✓ | – |
| SAM2 | – | – | ✓ |
| All | ✓ | ✓ | ✓ |

### Display Modes

| Mode | 2D BBox Overlay | 3D Spatial Labels |
|------|----------------|-------------------|
| BoundingBox2D | ✓ | – |
| SpatialLabel3D | – | ✓ |
| Both | ✓ | ✓ |

3D spatial labels **persist** across captures. Previous labels are not cleared
when a new capture completes — they accumulate in the scene so you can build
up a complete spatial map by walking around and pressing A repeatedly.

### Preview Distance

Controls the `ViewportRaycaster` fallback distance used when no AR plane or
physics surface is hit:

| Setting | Distance |
|---------|----------|
| Near | 1 m |
| Far | 3 m |
| Off | 0 m (disabled) |

## Quick Start

1. **Run the CV server**
   - See [cv-server/README.md](cv-server/README.md) for local Docker setup.

2. **Create a Server Config asset**
   - Project window → Create → Companion Scan → Server Config.
   - Set `baseUrl` to your server IP (example: `https://my-server.ts.net`).

3. **Open the Quest scene**
   - `Assets/CompanionScanLegacy/Scenes/CompanionScanLegacy.unity`.

4. **Wire the mapper components**
    - See [docs/quest-capture-gltf.md](docs/quest-capture-gltf.md) for the full component list.
    - Make sure `ControllerMenuBuilder` exists on a GameObject in the scene — it
       auto-discovers all pipeline/renderer/exporter references and builds the
       controller indicators + HUD at runtime.

5. **Build to Quest 3**
   - Menu → Companion Scan → Build Quest APK (or Build and Run).
   - Run Space Setup on the headset and grant `com.oculus.permission.USE_SCENE`.

6. **Use the controller**
   - **A** — capture (both pipelines run, 3D labels appear in world space).
   - **B** — switch model if you only want one AI model active.
   - **X** — change preview distance.
   - **Y** — export and upload glTF.
   - **Trigger** — toggle between 2D boxes, 3D labels, or both.

## glTF Export + Sync

- Exports `.gltf` + `.bin` and injects extras for:
  - Unified pins from both CV Server and Meta Scene (tagged by `source`)
  - CV-derived floorplan and bounds
  - Full Meta scene anchor data (labels, planes, volumes)
- Uploads to the local server at `POST /v1/gltf`.

## Testing

### Play-Mode Integration Tests

`Assets/CompanionScan/Tests/PlayMode/PerImageModelTests.cs` contains 9
integration tests that run all three models against 14 fixture images:

| Test | What it checks |
|------|---------------|
| Florence2_Caption_AllImages | Caption string returned for every image |
| Florence2_OD_AllImages | Object detection returns bboxes + labels |
| GDino_Detect_AllImages_BboxesValid | GroundingDINO bboxes are normalised 0–1 |
| GDino_Detect_AllImages_Anchors3D | Raycasted world positions are valid |
| SAM2_Segment_AllImages_MasksValid | SAM2 returns non-empty mask data |
| SAM2_Segment_AllImages_Anchors3D | Segmentation anchors raycast correctly |
| FullPipeline_PerImage_AllThreeModels | Full Florence→GDino→SAM2 pipeline |
| BBox2D_RenderingData_ValidForAllDetections | 2D rendering data is valid |
| Anchor3D_PinData_ValidForAllDetections | 3D pin data is valid |

Run via Unity Test Runner (Play Mode) or CI.

### CV Server Tests

```bash
cd cv-server
pytest
```

## Documentation

- World mapper pipeline + glTF schema: [docs/quest-capture-gltf.md](docs/quest-capture-gltf.md)
- Current build plan: [docs/plan.md](docs/plan.md)

## Legacy Sample (Cloud API)

The original NVIDIA cloud API sample lives under `Assets/CompanionScanLegacy/Scripts/` and is not the primary path anymore. The project now runs locally via the CV server.

## License

MIT — Free to use, modify and learn from.
