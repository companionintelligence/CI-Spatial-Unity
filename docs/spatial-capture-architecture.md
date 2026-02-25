# Spatial Capture Architecture

This document details how BoundingBox2D and SpatialLabel3D are produced, how they
should map to real-world space, and the key improvements required for accuracy.

## Data Flow Summary

1. Camera provider captures a frame and metadata.
2. CV server returns detections (2D bboxes + labels).
3. Each bbox is mapped to a 3D ray via `ViewportRaycaster`.
4. Rays resolve to world points (AR plane, physics, or fallback).
5. Results become `SpatialPin` entries with normalized semantic labels.
6. `BBoxRenderer` overlays 2D rectangles and `LabelRenderer` spawns 3D labels.

## BoundingBox2D Rendering

### Current Flow

- Source image texture is stored on capture.
- For each `DetectionResult`, `BBoxRenderer.RenderBoxes()`:
  - Converts raw bbox to normalized UVs (0-1) based on image width/height.
  - Scales UVs to the preview canvas dimensions.
  - Spawns a bbox UI prefab and label text.

### Required Guarantees

- Always use the exact image size returned by the camera provider when normalizing.
- Cache and reuse the source frame for the preview texture to avoid mismatched sizes.
- If the preview panel is world-space, ensure its RectTransform size matches the
  source image resolution to avoid skew.

### Improvements

- Store the exact `imageWidth` and `imageHeight` with each detection payload to
  guard against camera resolution changes.
- Add an explicit `scaleToFit` or `preserveAspect` mode for the preview canvas.

## SpatialLabel3D Placement

### Current Flow

- For each bbox, the center point is converted to normalized viewport coordinates.
- `ViewportRaycaster.TryRaycastViewport()` attempts:
  1) AR raycast (planes), then
  2) Physics raycast, then
  3) Fallback distance along the camera forward vector.
- The resulting world-space position becomes the `SpatialPin.position`.
- `LabelRenderer` spawns a text label or pin prefab at the position.

### Required Guarantees

- The camera pose and intrinsics must correspond to the same frame used for
  detection.
- The raycast origin must use the capture camera transform, not a later pose.
- Fallback distance must be explicit and logged in export metadata.

### Improvements

- Cache the capture camera pose and intrinsics with every snapshot.
- Use camera intrinsics to map the bbox center to a ray in camera space rather
  than relying solely on viewport coordinates when the aspect ratio is not 1:1.
- Prefer AR plane hits with semantic labels (floor, wall, table) to improve
  pin stability and alignment with scene understanding.

## Correctness Pitfalls

- Pose drift: moving between capture and raycast yields incorrect placement.
- Resolution mismatch: camera resolution changes break bbox normalization.
- Wrong camera: using `Camera.main` instead of the capture camera skews rays.

## Export Considerations

- Every `SpatialPin` should include a timestamp and the camera pose used to place it.
- For repeat captures, keep a stable ID per capture to correlate 2D and 3D outputs.
- If a fallback ray is used, record the fallback distance in extras.

## Recommended Next Steps

- Add a `CaptureFrame` struct that stores image bytes, size, camera pose, and
  intrinsics, and pass it through the pipeline.
- Implement temporal smoothing for repeated detections of the same label.
- Track per-label placement confidence based on raycast hit type.
