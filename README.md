# Unity-MetaXR-AI-ZeroShot
Quest 3 local world/space mapper that combines Meta scene understanding with a local CV server (Grounding DINO, SAM 2, Florence-2) to produce a glTF export with rich spatial metadata and sync it back to the server.

![florence-xr-Trim-Trim-ezgif com-optimize](https://github.com/user-attachments/assets/429c9837-574e-4857-8843-1727167f73c3)

## Overview

Two independent capture systems feed a **unified spatial pin store**:

- **Meta Scene Understanding** (Pipeline A) — on-device OVRAnchor queries for semantic labels, planes, volumes. No network needed. See [Meta Scene API docs](https://developers.meta.com/horizon/documentation/unity/unity-scene-overview/).
- **CV Server Pipeline** (Pipeline B) — passthrough camera frames → Florence2 → GroundingDINO → SAM2 via a local Docker server. Raycasts detections into world space.

Both pipelines write `SpatialPin` objects (tagged `CvServer` or `MetaScene`) to the shared `SpatialPinManager`. The glTF export merges data from both sources.

## Key Paths
- Scene: `Assets/XR-AI-ZeroShot/Scenes/XR-AI-ZeroShot.unity`
- World mapper scripts (HouseMapper): `Assets/HouseMapper/`
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
  └─ glTF export + sync → /v1/gltf

Local CV Server (Docker)
  - /v1/detect   (GroundingDINO)
  - /v1/segment  (Grounded SAM 2)
  - /v1/florence  (Florence-2)
  - /v1/gltf     (upload)
```

## Quick Start
1) Run the CV server
   - See [cv-server/README.md](cv-server/README.md) for local Docker setup.

2) Create a Server Config asset
   - Project window: Create -> HouseMapper -> Server Config.
   - Set `baseUrl` to your server IP (example: `http://192.168.1.100:8000`).

3) Open the Quest scene
   - `Assets/XR-AI-ZeroShot/Scenes/XR-AI-ZeroShot.unity`.

4) Wire the mapper components
   - See [docs/quest-capture-gltf.md](docs/quest-capture-gltf.md) for the component list and references.

5) Build to Quest 3
   - Run Space Setup and grant `com.oculus.permission.USE_SCENE`.
   - Press the capture button — triggers `CaptureAll()` which runs both Meta Scene + CV Server.
   - Press Export Sync to generate and upload the glTF.

## glTF Export + Sync

- Exports `.gltf` + `.bin` and injects extras for:
  - Unified pins from both CV Server and Meta Scene (tagged by `source`)
  - CV-derived floorplan and bounds
  - Full Meta scene anchor data (labels, planes, volumes)
- Uploads to the local server at `POST /v1/gltf`.

## Documentation
- World mapper pipeline + glTF schema: [docs/quest-capture-gltf.md](docs/quest-capture-gltf.md)
- Current build plan: [docs/plan.md](docs/plan.md)

## Legacy Sample (Cloud API)
The original NVIDIA cloud API sample lives under `Assets/XR-AI-ZeroShot/Scripts/` and is not the primary path anymore. The project now runs locally via the CV server.

## License
MIT - Free to use, modify and learn from.
