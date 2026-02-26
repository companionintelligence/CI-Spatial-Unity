# Quest 3 Capture Pipeline and glTF Export

This document describes the two spatial capture systems, how they feed a unified pin store, and the glTF export format.

## Architecture: Two Capture Systems, One Pin Store

The project has **two independent capture pipelines** that both write to the same `SpatialPinManager`.  Every `SpatialPin` carries a `PinSource` enum (`CvServer` or `MetaScene`) so downstream code can filter or merge them.

```text
┌──────────────────────────────────────────────────────┐
│                    SpatialCapturePipeline             │
│  (orchestrates both pipelines, calls CaptureAll)     │
├────────────────────────┬─────────────────────────────┤
│  Pipeline A            │  Pipeline B                 │
│  Meta Scene (on-device)│  CV Server (network)        │
│                        │                             │
│  OVRAnchor query       │  Camera JPEG capture        │
│  OVRRoomLayout         │   → Florence2 caption       │
│  OVRSemanticLabels     │   → Florence2 OD            │
│  OVRBounded2D / 3D    │   → GroundingDINO detect    │
│  OVRTriangleMesh       │   → SAM2 segment            │
│       │                │         │                   │
│       ▼                │         ▼                   │
│  MetaSceneSnapshot     │  DetectionResult /          │
│       │                │  SegmentationResult          │
│       ▼                │         │                   │
│  ConvertSnapshotToPins │  AddCvPinsFromDetections    │
│       │                │         │                   │
│       └────────┬───────┘─────────┘                   │
│                ▼                                     │
│        SpatialPinManager                             │
│        (unified List<SpatialPin>)                    │
│                │                                     │
│                ▼                                     │
│        SpatialMapExporter                            │
│        → glTF with merged extras                     │
│        → GltfSyncClient upload                       │
└──────────────────────────────────────────────────────┘
```

### Pipeline A — Meta Scene Understanding (on-device)

Runs entirely on the Quest headset using the [Meta Scene API](https://developers.meta.com/horizon/documentation/unity/unity-scene-overview/).  No network required.

| Step | What happens |
| --- | --- |
| 1 | `MetaSceneCollector.CaptureSnapshotAsync()` queries `OVRAnchor.FetchAnchorsAsync` for rooms with `OVRRoomLayout` |
| 2 | For each room, fetches child anchors via `OVRAnchorContainer` |
| 3 | Per anchor reads: `OVRLocatable` (world pose), `OVRSemanticLabels` (classification), `OVRBounded2D` (plane), `OVRBounded3D` (volume), `OVRTriangleMesh` (mesh counts) |
| 4 | Stores everything in a `MetaSceneSnapshot` (list of `MetaSceneAnchorSnapshot`) |
| 5 | `ConvertSnapshotToPins()` converts each anchor to a `SpatialPin` with `source = PinSource.MetaScene` |
| 6 | Pins added to `SpatialPinManager` |

**Prerequisites:**

- User must run **Space Setup** on the Quest headset
- Permission `com.oculus.permission.USE_SCENE` must be granted
- `MetaSceneCollector.trackingSpace` must reference the XR tracking space `Transform`

**Data captured per anchor:** UUID, semantic labels, world position + rotation, plane bounds + boundary polygon, volume bounds, mesh vertex/triangle counts.

### Pipeline B — CV Server (network)

Captures a camera frame on Quest and sends it to a local Docker CV server for AI inference. Detected objects are raycasted into world space.

| Step | What happens |
| --- | --- |
| 1 | `ICameraProvider.GetCurrentFrameJpeg()` captures a passthrough camera frame |
| 2 | A `CaptureFrame` is built with image size, pose, intrinsics, session ID, and frame index |
| 3 | Florence2 caption (`<MORE_DETAILED_CAPTION>`) → `LastCaption` |
| 4 | Florence2 OD (`<OD>`) → extract unique labels |
| 5 | GroundingDINO detect (prompt from Florence labels or fallback) → `DetectionResult` with normalised bboxes |
| 6 | For each detection: `ViewportRaycaster.TryRaycastCapture()` → world hit point (intrinsics-based when available) |
| 7 | Prefer semantic plane intersections (floor, wall, table) before fallback distance |
| 8 | `SemanticLabelMapper.MapToMetaLabel()` normalises CV labels to Meta vocabulary |
| 9 | Each detection → `SpatialPin` with `source = PinSource.CvServer` → `SpatialPinManager.AddPin()` |
| 10 | SAM2 segment → `SegmentationResult` → optional mask overlay via `MaskRenderer` |

**Prerequisites:**

- CV server running (Docker) at the URL in `ServerConfig`
- Requests use timeout/retry settings from `ServerConfig`
- A camera provider implementing `ICameraProvider`
  - Quest: prefer Meta OpenXR + ARFoundation PCA (`MobileCameraProvider`)
  - Desktop: `DesktopCameraProvider`
- `ViewportRaycaster` for 2D→3D raycasting
- Optional: MRUK `EnvironmentRaycastManager` for depth-based raycasts (can be required via `ViewportRaycaster.requireEnvironmentRaycast`)
- On Quest, PCA requires passthrough camera permission

### Camera Preview + Frustum

- `BBoxRenderer` spawns a world-space camera preview panel and overlays 2D boxes.
- `SnapshotFrustumVisualizer` draws rays to the camera frustum corners on capture.

### Triggering Capture

`SpatialCapturePipeline` provides three public methods:

| Method | What it does |
| --- | --- |
| `CaptureAll()` | Runs **both** pipelines: Meta Scene first, then CV Server |
| `CaptureAndPin()` | Runs only the **CV Server** pipeline |
| `CaptureMetaScene()` | Runs only the **Meta Scene** pipeline |

These are wired automatically by the XR controller input system.

### XR Controller Input

All interaction uses physical Quest controller buttons via `XRControllerInput`
→ `ControllerMenuUI`. No world-space UI panels are needed.

| Button | Controller | Colour | Action |
| --- | --- | --- | --- |
| **A** | Right | Red | Capture (runs `CaptureAll()` with current model preset) |
| **B** | Right | Blue | Cycle model: Florence → GDino → SAM2 |
| **X** | Left | Yellow | Cycle preview distance: Near (1 m) → Far (3 m) → Off |
| **Y** | Left | Green | Save & Export glTF |
| **Trigger** | Either | Orange | Cycle display: BBox2D → Label3D → Both |

Coloured circle indicators and labels are spawned on the controller models at
runtime by `ControllerMenuBuilder`. A wrist HUD on the left controller shows
the current model, display mode, preview distance, and busy state.
It also reports raycast status (intrinsics vs pose-only) and fallback hit count
for the most recent capture.
Network timeout/retry settings are shown from the active `ServerConfig`.

3D spatial labels **persist** across captures by default, but the
`SpatialPinManager` can prune stale pins when lifecycle is enabled.

## SpatialPin — Unified Data Type

Both systems produce `SpatialPin` objects with these fields:

```csharp
public class SpatialPin
{
    // Identity
    string   label;            // raw detection label or Meta class
    string   semanticLabel;    // normalised to Meta vocabulary
    float    score;            // confidence (1.0 for Meta ground-truth)
    PinSource source;          // CvServer or MetaScene

    // Spatial
    Vector3    position;       // world-space
    Vector3    normal;         // surface normal
    Quaternion rotation;       // anchor rotation (MetaScene)

    // 2D (CV-only)
    SerializableRect bboxPixels;
    SerializableRect bboxNormalized;

    // Geometry (MetaScene-only)
    Vector3 planeSize;
    Vector3 volumeSize;
    string  anchorUuid;

    // Metadata
    string sourcePrompt;
    double timestamp;
    string captureId;
    string captureSessionId;
    int    captureFrameIndex;
    float  placementConfidence;
    RaycastHitType hitType;
    int    trackId;
}
```

`SpatialPinManager` provides filtered views: `.Pins` (all), `.CvPins`, `.MetaScenePins`.
When lifecycle is enabled, stale pins are removed and updates are merged by `trackId`.

## Key Components

Components in `Assets/CompanionScan/`:

| Component | Role |
| --- | --- |
| `SpatialCapturePipeline` | Orchestrator — runs CV pipeline (Florence2→GDino→SAM2) and/or Meta Scene capture |
| `MetaSceneCollector` | Queries OVRAnchor/OVRRoomLayout, converts to `SpatialPin`s |
| `ViewportRaycaster` | Converts image-space UV → world-space hit via Physics or ARRaycast. Exposes `FallbackDistance` for runtime control. |
| `SpatialPinManager` | Stores + spawns pins from both systems |
| `SpatialPin` | Unified data type with `PinSource` enum |
| `SemanticLabelMapper` | Normalises CV labels to Meta vocabulary |
| `BoundaryProvider` | Reads XR guardian boundary points |
| `FloorplanBuilder` | Builds floor polygon + mesh from pins/boundary |
| `SpatialMapExporter` | Exports glTF with unified extras from both systems |
| `GltfSyncClient` | Uploads exported glTF to the CV server |
| `CVServerClient` | HTTP client for `/v1/detect`, `/v1/segment`, `/v1/florence` |
| `ICameraProvider` | Camera abstraction (Quest / Desktop / Mobile implementations) |
| `XRControllerInput` | Polls OVRInput and dispatches button presses to `ControllerMenuUI` |
| `ControllerMenuUI` | State machine: model preset, display mode, preview distance |
| `ControllerMenuBuilder` | Builds coloured button indicators, wrist HUD, and wires everything at runtime |
| `LabelRenderer` | 3D label spawning with optional persistence across captures |
| `BBoxRenderer` | 2D bounding-box overlay rendering |

## Scene Wiring

1. Add a camera provider that implements `ICameraProvider`.
   - Quest: prefer Meta OpenXR + ARFoundation PCA (`MobileCameraProvider`).
   - Desktop: `DesktopCameraProvider`.
2. Add an empty GameObject called `SpatialMapping` and add:
   - `SpatialCapturePipeline`
   - `ViewportRaycaster`
   - `SpatialPinManager`
   - `MetaSceneCollector`
   - `BoundaryProvider`
   - `SpatialMapExporter`
   - `GltfSyncClient`
3. Set references:
   - `SpatialCapturePipeline`
     - `Server Config` → your `ServerConfig` ScriptableObject
     - `Camera Provider Source` → your PCA provider (`MobileCameraProvider`)
     - `Raycaster` → the `ViewportRaycaster` on the same GameObject
     - `Pin Manager` → the `SpatialPinManager` on the same GameObject
     - `Meta Scene Collector` → the `MetaSceneCollector` on the same GameObject
   - `ViewportRaycaster`
     - `Target Camera` → main XR camera
   - `SpatialPinManager`
     - `Pin Prefab` → any world-space label prefab (TextMeshPro supported)
   - `MetaSceneCollector`
     - `Tracking Space` → XR tracking origin Transform
   - `SpatialMapExporter`
     - `Export Root` → root object to export
     - `Pin Manager` → the `SpatialPinManager`
     - `Boundary Provider` → the `BoundaryProvider`
     - `Meta Scene Collector` → the `MetaSceneCollector`
     - `Sync Client` → the `GltfSyncClient`
   - `GltfSyncClient`
     - `Server Config` → same `ServerConfig` ScriptableObject
4. Add a `ControllerMenuBuilder` component to any GameObject in the scene.
   It auto-discovers all pipeline/renderer/exporter singletons and builds:
   - `ControllerMenuUI` — state machine (model preset, display mode, preview distance)
   - `XRControllerInput` — OVRInput polling → dispatches to `ControllerMenuUI`
   - Coloured circle indicators on both controller anchors
   - Wrist HUD panel on the left controller

   No manual button wiring is needed. The builder uses `FindAnyObjectByType`
   to locate `SpatialCapturePipeline`, `SpatialMapExporter`, `BBoxRenderer`,
   `LabelRenderer`, etc.

## Prompts and Categories

The CV pipeline uses a multi-stage prompt strategy:

1. **Florence2 caption** (`<MORE_DETAILED_CAPTION>`) — free-form scene description
2. **Florence2 OD** (`<OD>`) — extracts object labels
3. **GroundingDINO prompt** — built automatically from Florence2 labels (e.g. `"couch . table . lamp ."`) with a configurable fallback: `"furniture . object ."`
4. **SAM2 prompt** — built from GroundingDINO labels filtered by score, or Florence labels, or the fallback

Each prompt stage feeds the next.  Update `florenceCaptionTask`, `florenceDetectionTask`, and `groundingDinoFallbackPrompt` in the Inspector to customise.

## CV Server

`ServerConfig` should point to the CV server (HTTPS preferred for Quest builds):

```text
https://<server-hostname>
```

`SpatialCapturePipeline` (Pipeline B) uses:

- `FlorenceAsync` for captions and object detection
- `DetectAsync` for GroundingDINO bounding-box detection
- `SegmentAsync` for SAM2 mask segmentation

## Meta Scene Understanding

Meta scene understanding provides semantic labels and surface geometry (planes and volumes). Supported labels include:

```text
CEILING, DOOR_FRAME, FLOOR, INVISIBLE_WALL_FACE, WALL_ART, WALL_FACE, WINDOW_FRAME,
COUCH, TABLE, BED, LAMP, PLANT, SCREEN, STORAGE, GLOBAL_MESH, OTHER
```

`MetaSceneCollector` queries the scene model and stores anchors with labels, transforms, plane bounds, volume bounds, and optional boundary points.
This requires the `com.oculus.permission.USE_SCENE` permission to be granted on device.

## Scene Mesh Export

Meta scene triangle meshes can be exported into the glTF when `MetaSceneCollector.includeMeshes`
and `SpatialMapExporter.includeSceneMeshes` are enabled. During export, scene mesh GameObjects
are created from `OVRTriangleMesh` data and included in the exported glTF.

Implementation notes:

- Meshes are pulled from `OVRTriangleMesh.TryGetCounts` + `TryGetMesh` and converted into
  `UnityEngine.Mesh` instances at export time.
- Mesh objects are created under the export root for the duration of the export and destroyed
  immediately after writing the glTF.

References:

- <https://developers.meta.com/horizon/design/mr-design-scene>
- <https://developers.meta.com/horizon/documentation/unity/unity-scene-build-mixed-reality/>

## Implementation Improvements

- Prefer PCA camera capture on Quest (Meta OpenXR + ARFoundation) to avoid WebCamTexture
  compatibility issues.
- Track camera intrinsics when available and include them in `SnapshotFrameStore` for
  more accurate 2D→3D projection.
- Consider smoothing / temporal filtering for repeated captures to reduce anchor jitter.

## Design Alignment

The mapping pipeline is aligned with Meta scene understanding guidance:

- Prefer placement on classified planes (floor, wall, table) for stable anchors.
- Use semantic labels to drive content rules (only place on FLOOR, only attach to WALL_FACE, etc.).
- Favor occlusion and surface alignment to make pins and overlays feel grounded.

## glTF Export

`SpatialMapExporter.Export()` writes a `.gltf` + `.bin` under:

```text
Application.persistentDataPath/exports
```

## glTF Sync Upload

`SpatialMapExporter.ExportAndSync()` calls `GltfSyncClient` to upload the exported `.gltf` and `.bin` back to the CV server.
The server expects a multipart POST at:

```text
POST /v1/gltf
```

The payload includes:

- `gltf` (file) - the `.gltf` JSON
- `bin` (file) - the `.bin` buffer (optional)
- `extras` (field) - the extras JSON (optional)

Set `GLTF_UPLOAD_DIR` in the server container to control where uploads land (default: `uploads/gltf`).

It also injects a JSON blob into the root `extras` with unified pin data from both systems:

```json
{
  "schema_version": "1.0",
  "timestamp_utc": "2025-01-01T12:00:00.0000000Z",
  "units": "meters",
  "cv_mapping": {
    "bounds": {
      "center": [0.0, 1.2, 0.0],
      "size": [4.2, 2.6, 3.8]
    },
    "boundary": [[-1,0,-1],[1,0,-1],[1,0,1],[-1,0,1]],
    "floorplan": [[-1,0,-1],[1,0,-1],[1,0,1],[-1,0,1]],
    "pins": [
      {
        "label": "couch",
        "semantic_label": "COUCH",
        "score": 0.83,
        "source": "CvServer",
        "position": [0.5, 0.4, 1.2],
        "normal": [0, 1, 0],
        "rotation": [0, 0, 0, 1],
        "bbox_pixels": [128, 96, 256, 192],
        "bbox_normalized": [0.2, 0.15, 0.4, 0.3],
        "plane_size": [0, 0, 0],
        "volume_size": [0, 0, 0],
        "anchor_uuid": "",
        "prompt": "couch . table . lamp .",
        "timestamp": 123.45
      },
      {
        "label": "COUCH",
        "semantic_label": "COUCH",
        "score": 1.0,
        "source": "MetaScene",
        "position": [0.2, 0.0, 1.1],
        "normal": [0, 1, 0],
        "rotation": [0.0, 0.0, 0.0, 1.0],
        "bbox_pixels": [0, 0, 0, 0],
        "bbox_normalized": [0, 0, 0, 0],
        "plane_size": [1.6, 0.6, 0.0],
        "volume_size": [1.6, 0.6, 0.8],
        "anchor_uuid": "550e8400-e29b-41d4-a716-446655440000",
        "prompt": "meta_scene_understanding",
        "timestamp": 120.0
      }
    ]
  },
  "meta_scene": {
    "available": true,
    "supported_labels": ["CEILING","DOOR_FRAME","FLOOR","INVISIBLE_WALL_FACE","WALL_ART","WALL_FACE","WINDOW_FRAME","COUCH","TABLE","BED","LAMP","PLANT","SCREEN","STORAGE","GLOBAL_MESH","OTHER"],
    "anchors": [
      {
        "uuid": "550e8400-e29b-41d4-a716-446655440000",
        "labels": ["COUCH"],
        "position": [0.2, 0.0, 1.1],
        "rotation": [0.0, 0.0, 0.0, 1.0],
        "has_plane": true,
        "plane_center": [0.0, 0.0, 0.0],
        "plane_size": [1.6, 0.6, 0.0],
        "plane_boundary": [],
        "has_volume": true,
        "volume_center": [0.0, 0.3, 0.0],
        "volume_size": [1.6, 0.6, 0.8],
        "has_mesh": false,
        "mesh_vertex_count": 0,
        "mesh_triangle_count": 0
      }
    ]
  }
}
```

The `pins` array contains entries from **both** systems.  Use the `source` field to distinguish `"CvServer"` vs `"MetaScene"` pins.  The `meta_scene` block contains the full raw anchor data for downstream processing.

## Notes

- Boundary points come from `XRInputSubsystem.TryGetBoundaryPoints` when available.
- If no boundary is available, the floorplan uses a convex hull over pin positions.
- The floor mesh is optional and is created just for export.
- Pins are spawned in world space and included in the exported scene.
- Use `SpatialMapExporter.ExportAndSync()` to upload the glTF to the server via `GltfSyncClient`.
- Both capture systems write `SpatialPin` objects with `PinSource.CvServer` or `PinSource.MetaScene`.
- `SpatialPinManager.ClearPins(PinSource)` can selectively clear one system's pins.
- Meta Scene pins have `score = 1.0` (ground-truth) and carry `planeSize`/`volumeSize`/`anchorUuid`.
- CV Server pins have `bboxPixels`/`bboxNormalized` and are raycasted via `ViewportRaycaster`.
- `SemanticLabelMapper` normalises both CV and Meta labels to the same Meta vocabulary.
- The Meta Scene API reference: <https://developers.meta.com/horizon/documentation/unity/unity-scene-overview/>
