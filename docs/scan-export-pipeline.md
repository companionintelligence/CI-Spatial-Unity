# Scan and Export Pipeline

This document provides a detailed breakdown of how scans are captured, processed,
and exported to glTF with unified metadata.

## Capture Pipeline (Quest Runtime)

### Step 1: Acquire Frame

- `ICameraProvider.GetCurrentFrameJpeg()` returns JPEG bytes.
- The capture metadata should include:
  - width/height
  - camera pose
  - camera intrinsics

### Step 2: CV Server Processing

- Florence2 caption (optional) produces a textual description.
- Florence2 OD (optional) yields coarse labels.
- GroundingDINO produces precise bboxes.
- SAM2 segmentation produces masks.
- Requests use timeout/retry settings from `ServerConfig`.

### Step 3: 2D + 3D Outputs

- `BBoxRenderer` overlays 2D rectangles onto a preview canvas.
- `ViewportRaycaster` maps bbox centers to a 3D position.
- Optional: MRUK `EnvironmentRaycastManager` provides depth-based raycasts (can be required via `ViewportRaycaster.requireEnvironmentRaycast`).
- `LabelRenderer` spawns a world-space label at each 3D position.

### Step 4: Meta Scene Capture (Parallel)

- `MetaSceneCollector.CaptureSnapshotAsync()` queries Scene Model anchors.
- Anchors include semantic labels, planes, volumes, and optional meshes.
- Each anchor is converted into a `SpatialPin` entry.

## glTF Export Pipeline

### Step 1: Build Export Scene

- `SpatialMapExporter` creates an export root.
- Optional: floor mesh created from boundary + pins.
- Optional: scene meshes created from `OVRTriangleMesh` data.

### Step 2: Export Files

- UnityGLTF writes `.gltf` + `.bin` files into
  `Application.persistentDataPath/exports/<spaceId>/`.

### Step 3: Extras Injection

- `cv_mapping` includes pins, boundary, floorplan, and snapshot metadata.
- `meta_scene` includes all Scene Model anchor data.
- `snapshots` include JPEG paths and frustum metadata.

### Step 4: Upload

- `GltfSyncClient` uploads to `/v1/gltf` with multipart payload.

## Implemented Improvements

- Capture metadata is now stored per frame (pose + intrinsics).
- Per-capture IDs are attached to pins and exported in glTF extras.
- Placement confidence now combines model score and raycast hit quality.
- Pin de-duplication merges nearby pins with the same semantic label.
- Semantic plane hits (floor, wall, table) are preferred when available.
- Pins carry capture session/frame IDs and per-label track IDs.
- Pin lifecycle prunes stale pins and merges updates by track ID.

## Remaining Improvements

- Add a regression test for semantic plane projection to validate hit ordering.
