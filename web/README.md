# Companion Scan — Web Version

Web port of the Companion Scan Quest 3 app, built with the
**[Meta Immersive Web SDK (IWSDK)](https://iwsdk.dev)**.

Runs in any WebXR-capable browser. On a Meta Quest browser it launches in
passthrough AR mode (immersive-ar), offering the same scan → detect → export
workflow as the Unity build without requiring a native APK.

## Features

| Feature | Implementation |
|---------|---------------|
| **Passthrough AR / VR** | `@iwsdk/core` — `SessionMode.ImmersiveAR / ImmersiveVR` |
| **Spatial pin labels** | Three.js `Sprite` labels in world space |
| **CV server client** | `CVServerClient` — fetch-based port of Unity `CVServerClient.cs` |
| **Florence-2 caption / OD** | `POST /v1/florence` |
| **Grounding DINO detect** | `POST /v1/detect` |
| **SAM 2 segment** | `POST /v1/segment` |
| **XR plane detection** | IWSDK `SceneUnderstandingSystem` + `XRPlane` component |
| **XR mesh overlay** | IWSDK `XRMesh` component → Three.js semi-transparent mesh |
| **glTF export** | Three.js `GLTFExporter` + pin metadata injected as `extras` |
| **glTF upload** | `POST /v1/gltf` multipart form |
| **Browser download fallback** | `<a download>` when server unavailable |
| **Quest controller input** | WebXR Gamepad API (A/B/X/Y + trigger + grip buttons) |
| **HUD overlay** | DOM-based status bar mirroring wrist HUD |

## Architecture

```
web/src/
├── core/
│   ├── CVServerClient.ts   REST client (detect / segment / florence / gltf)
│   ├── ServerConfig.ts     Base URL + thresholds (localStorage override)
│   └── types.ts            Shared data types (DetectionResult, etc.)
├── mapping/
│   ├── SpatialPin.ts       SpatialPin type + makeSpatialPin helper
│   ├── SpatialPinManager.ts Central pin store (dedup, lifecycle, labels)
│   └── ScanningPipeline.ts Florence → GDino → SAM2 orchestration
├── scene/
│   ├── XRScene.ts          IWSDK world init, ECS systems, action dispatch
│   ├── CameraCapture.ts    WebXR camera / getUserMedia JPEG capture
│   └── GltfMappingLayer.ts GLTFLoader overlay for mapping meshes
├── export/
│   └── GltfExporter.ts     Three.js GLTFExporter + upload to /v1/gltf
└── ui/
    ├── ControllerInput.ts  WebXR Gamepad → pipeline actions
    └── HudDisplay.ts       DOM HUD update
```

## Controller Mapping (Quest Browser)

Mirrors the Unity `XRControllerInput.cs` button mapping:

| Button | Action |
|--------|--------|
| **A** (right button 4) | Capture (runs active model) |
| **B** (right button 5) | Cycle model: Florence → GDino → SAM2 |
| **X** (left button 4) | Cycle preview distance: Near → Far → Off |
| **Y** (left button 5) | Save & Export glTF |
| **Index Trigger** (button 0) | Cycle display mode |
| **Grip** (button 1) | Preview glTF metadata |

## Quick Start

### Prerequisites

- Node.js 18+
- cv-server running locally (`cd cv-server && docker compose up`)

### Development

```bash
cd web
npm install
npm run dev
```

Open `http://localhost:5173` in your browser.

The dev server proxies `/v1/*` requests to `http://localhost:8000`, so you
don't need CORS configuration during development.

### Production build

```bash
npm run build
# Serve from web/dist/
```

### Deploying to Meta Quest

1. Build the app (`npm run build`).
2. Serve `web/dist/` over HTTPS (required for WebXR).
3. Open the URL in the Quest Browser.
4. Tap **Enter AR** to start passthrough scanning.

### Configuration

Set the CV server URL in the **Settings** panel (top right of the screen).
The URL is persisted in `localStorage` and applied to all subsequent requests.

| Setting | Default | Notes |
|---------|---------|-------|
| CV Server Base URL | `http://localhost:8000` | Must be reachable from the device |
| Box Threshold | `0.35` | Grounding DINO confidence threshold |
| Text Threshold | `0.25` | Grounding DINO text threshold |

## Differences from the Unity Build

| Aspect | Unity | Web |
|--------|-------|-----|
| Session mode | OVR (native) | WebXR (`immersive-ar`) |
| Scene understanding | OVRAnchor | IWSDK `SceneUnderstandingSystem` |
| Camera capture | `QuestCameraProvider` | `getUserMedia` / WebXR Camera Access API |
| Pin labels | TextMeshPro prefabs | Three.js `Sprite` (canvas texture) |
| glTF export | UnityGLTF | Three.js `GLTFExporter` |
| Controller input | OVRInput | WebXR Gamepad API |

## Dependencies

| Package | Purpose |
|---------|---------|
| `@iwsdk/core` | Meta Immersive Web SDK — WebXR ECS runtime |
| `three` | 3D rendering, glTF export/import |
| `vite` | Build tool + dev server |
| `typescript` | Type safety |
