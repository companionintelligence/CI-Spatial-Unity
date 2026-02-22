# Unity-MetaXR-AI-ZeroShot
Quest 3 local world/space mapper that combines Meta scene understanding with a local CV server (Grounding DINO, SAM 2, Florence-2) to produce a glTF export with rich spatial metadata and sync it back to the server.

![florence-xr-Trim-Trim-ezgif com-optimize](https://github.com/user-attachments/assets/429c9837-574e-4857-8843-1727167f73c3)

## Overview
- Runs entirely on your local network: Quest 3 capture + CV server in Docker.
- Captures Quest camera frames, runs detection/segmentation/captioning, and raycasts pins into world space.
- Captures Meta scene anchors (semantic labels, planes, volumes) from Space Setup.
- Exports a glTF with combined extras and uploads it back to the server when the user taps Export Sync.

## Key Paths
- Scene: `Assets/XR-AI-ZeroShot/Scenes/XR-AI-ZeroShot.unity`
- World mapper scripts (HouseMapper): `Assets/HouseMapper/`
- CV server: `cv-server/`

## Architecture
```
Quest 3 App
  - Camera capture
  - CV server requests
  - Pin raycasts + labels
  - Meta scene anchors
  - glTF export + sync

Local CV Server (Docker)
  - /v1/detect (Grounding DINO)
  - /v1/segment (Grounded SAM 2)
  - /v1/florence (Florence-2)
  - /v1/gltf (upload)
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
   - Press the capture button to pin objects.
   - Press Export Sync to generate and upload the glTF.

## glTF Export + Sync
- Exports `.gltf` + `.bin` and injects extras for:
  - CV mapping (pins, floorplan, bounds)
  - Meta scene anchors (labels, planes, volumes)
- Uploads to the local server at `POST /v1/gltf`.

## Documentation
- World mapper pipeline + glTF schema: [docs/quest-capture-gltf.md](docs/quest-capture-gltf.md)
- Current build plan: [docs/plan.md](docs/plan.md)

## Legacy Sample (Cloud API)
The original NVIDIA cloud API sample lives under `Assets/XR-AI-ZeroShot/Scripts/` and is not the primary path anymore. The project now runs locally via the CV server.

## License
MIT - Free to use, modify and learn from.
