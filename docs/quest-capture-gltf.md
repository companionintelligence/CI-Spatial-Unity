# Quest 3 Capture Pipeline and glTF Export

This document describes the Quest 3 capture pipeline, Meta scene understanding capture, pinning workflow, boundary/floorplan tracking, and glTF export with embedded extras.

## Overview

The pipeline captures a camera frame on Quest, runs detection/segmentation/captioning against the CV server, raycasts detections into world space, spawns pins, optionally captures Meta scene anchors (semantic labels, planes, volumes), and exports a glTF with spatial metadata embedded in `extras`.

Key components added in `Assets/HouseMapper`:

- `SpatialCapturePipeline` - captures frames and runs CV server requests.
- `ViewportRaycaster` - converts image-space detections into world-space hits.
- `SpatialPinManager` - tracks and spawns pins.
- `SemanticLabelMapper` - normalizes CV labels to Meta semantic labels.
- `BoundaryProvider` - reads XR boundary points.
- `FloorplanBuilder` - builds floor polygon and mesh.
- `MetaSceneCollector` - captures Meta scene anchors (labels, planes, volumes).
- `SpatialMapExporter` - exports glTF and writes extras.
- `GltfSyncClient` - uploads glTF back to the server.

## Scene Wiring

1. Add a camera provider that implements `ICameraProvider` (Quest uses `QuestCameraProvider`).
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
     - `Server Config` -> `Assets/HouseMapper/Data/ServerConfig` (or create one)
     - `Camera Provider Source` -> your `QuestCameraProvider`
     - `Raycaster` -> the `ViewportRaycaster` on the same GameObject
     - `Pin Manager` -> the `SpatialPinManager` on the same GameObject
   - `ViewportRaycaster`
     - `Target Camera` -> main XR camera
   - `SpatialPinManager`
     - `Pin Prefab` -> any world-space label prefab (TextMeshPro supported)
   - `SpatialMapExporter`
     - `Export Root` -> root object to export (pins + optional floor mesh)
     - `Pin Manager` -> the `SpatialPinManager`
     - `Boundary Provider` -> the `BoundaryProvider`
     - `Meta Scene Collector` -> the `MetaSceneCollector`
     - `Sync Client` -> the `GltfSyncClient`
   - `GltfSyncClient`
     - `Server Config` -> `Assets/HouseMapper/Data/ServerConfig`

4. Trigger capture and export from input:
   - Use `TouchControllerEvent` to call `SpatialCapturePipeline.CaptureAndPin()` on button press.
  - Add a UI button to call `SpatialMapExporter.ExportAndSync()` when ready.

## Prompts and Categories

The default detection prompt is:

```
couch, bed, screen, table, lamp, plant, storage, door frame, window frame, wall, floor, ceiling, other
```

Update `SpatialCapturePipeline.detectionPrompt` to match your taxonomy. Segmentation uses:

```
wall. floor. ceiling.
```

## CV Server

`ServerConfig` should point to the CV server container:

```
http://<server-ip>:8000
```

`SpatialCapturePipeline` uses:

- `DetectAsync` for object detection
- `SegmentAsync` for wall/floor/ceiling masks
- `FlorenceAsync` for captions

## Meta Scene Understanding

Meta scene understanding provides semantic labels and surface geometry (planes and volumes). Supported labels include:

```
CEILING, DOOR_FRAME, FLOOR, INVISIBLE_WALL_FACE, WALL_ART, WALL_FACE, WINDOW_FRAME,
COUCH, TABLE, BED, LAMP, PLANT, SCREEN, STORAGE, GLOBAL_MESH, OTHER
```

`MetaSceneCollector` queries the scene model and stores anchors with labels, transforms, plane bounds, volume bounds, and optional boundary points.
This requires the `com.oculus.permission.USE_SCENE` permission to be granted on device.

## Design Alignment

The mapping pipeline is aligned with Meta scene understanding guidance:

- Prefer placement on classified planes (floor, wall, table) for stable anchors.
- Use semantic labels to drive content rules (only place on FLOOR, only attach to WALL_FACE, etc.).
- Favor occlusion and surface alignment to make pins and overlays feel grounded.

## glTF Export

`SpatialMapExporter.Export()` writes a `.gltf` + `.bin` under:

```
Application.persistentDataPath/exports
```

## glTF Sync Upload

`SpatialMapExporter.ExportAndSync()` calls `GltfSyncClient` to upload the exported `.gltf` and `.bin` back to the CV server.
The server expects a multipart POST at:

```
POST /v1/gltf
```

The payload includes:

- `gltf` (file) - the `.gltf` JSON
- `bin` (file) - the `.bin` buffer (optional)
- `extras` (field) - the extras JSON (optional)

Set `GLTF_UPLOAD_DIR` in the server container to control where uploads land (default: `uploads/gltf`).

It also injects a JSON blob into the root `extras` with both CV and Meta scene data:

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
        "source": "cv_server",
        "score": 0.83,
        "position": [0.5, 0.4, 1.2],
        "normal": [0, 1, 0],
        "bbox_pixels": [128, 96, 256, 192],
        "bbox_normalized": [0.2, 0.15, 0.4, 0.3],
        "prompt": "couch, bed, screen, table, lamp, plant, storage, door frame, window frame, wall, floor, ceiling, other",
        "timestamp": 123.45
      }
    ]
  },
  "meta_scene": {
    "available": true,
    "supported_labels": ["CEILING","DOOR_FRAME","FLOOR","INVISIBLE_WALL_FACE","WALL_ART","WALL_FACE","WINDOW_FRAME","COUCH","TABLE","BED","LAMP","PLANT","SCREEN","STORAGE","GLOBAL_MESH","OTHER"],
    "anchors": [
      {
        "uuid": "...",
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

## Notes

- Boundary points come from `XRInputSubsystem.TryGetBoundaryPoints` when available.
- If no boundary is available, the floorplan uses a convex hull over pin positions.
- The floor mesh is optional and is created just for export.
- Pins are spawned in world space and included in the exported scene.
- Use `SpatialMapExporter.ExportAndSync()` to upload the glTF to the server via `GltfSyncClient`.
