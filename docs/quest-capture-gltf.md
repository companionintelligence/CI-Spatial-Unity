# Quest 3 Capture Pipeline and glTF Export

This document describes the Quest 3 capture pipeline, pinning workflow, boundary/floorplan tracking, and glTF export with embedded extras.

## Overview

The pipeline captures a camera frame on Quest, runs detection/segmentation/captioning against the CV server, raycasts detections into world space, spawns pins, and exports a glTF with spatial metadata embedded in `extras`.

Key components added in `Assets/HouseMapper`:

- `SpatialCapturePipeline` - captures frames and runs CV server requests.
- `ViewportRaycaster` - converts image-space detections into world-space hits.
- `SpatialPinManager` - tracks and spawns pins.
- `BoundaryProvider` - reads XR boundary points.
- `FloorplanBuilder` - builds floor polygon and mesh.
- `SpatialMapExporter` - exports glTF and writes extras.

## Scene Wiring

1. Add a camera provider that implements `ICameraProvider` (Quest uses `QuestCameraProvider`).
2. Add an empty GameObject called `SpatialMapping` and add:
   - `SpatialCapturePipeline`
   - `ViewportRaycaster`
   - `SpatialPinManager`
   - `BoundaryProvider`
   - `SpatialMapExporter`
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

4. Trigger capture from input:
   - Use `TouchControllerEvent` to call `SpatialCapturePipeline.CaptureAndPin()` on button press.
   - Add a UI button to call `SpatialMapExporter.Export()` when ready.

## Prompts and Categories

The default detection prompt is:

```
couch, bed, screen, table, lamp, plant, wall, floor, ceiling, other
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

## glTF Export

`SpatialMapExporter.Export()` writes a `.gltf` + `.bin` under:

```
Application.persistentDataPath/exports
```

It also injects a JSON blob into the root `extras`:

```json
{
  "schema_version": "1.0",
  "timestamp_utc": "2025-01-01T12:00:00.0000000Z",
  "units": "meters",
  "bounds": {
    "center": [0.0, 1.2, 0.0],
    "size": [4.2, 2.6, 3.8]
  },
  "boundary": [[-1,0,-1],[1,0,-1],[1,0,1],[-1,0,1]],
  "floorplan": [[-1,0,-1],[1,0,-1],[1,0,1],[-1,0,1]],
  "pins": [
    {
      "label": "couch",
      "score": 0.83,
      "position": [0.5, 0.4, 1.2],
      "normal": [0, 1, 0],
      "bbox_pixels": [128, 96, 256, 192],
      "bbox_normalized": [0.2, 0.15, 0.4, 0.3],
      "prompt": "couch, bed, screen, table, lamp, plant, wall, floor, ceiling, other",
      "timestamp": 123.45
    }
  ]
}
```

## Notes

- Boundary points come from `XRInputSubsystem.TryGetBoundaryPoints` when available.
- If no boundary is available, the floorplan uses a convex hull over pin positions.
- The floor mesh is optional and is created just for export.
- Pins are spawned in world space and included in the exported scene.
