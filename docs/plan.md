# Current Plan

## Goal

Build Companion Scan clients for Quest 3 and WebXR that capture CV detections
and scene anchors, export a glTF with unified extras, and sync it back to the
local server.

## Milestones

1. Local CV server stable — **done**
   - Docker build + GPU inference
   - Endpoints: detect, segment, florence, gltf upload
   - Florence2 caption/OD tasks validated

2. Quest capture + pinning — **done**
   - Camera frame capture via `ICameraProvider`
   - Sequential pipeline: Florence2 → GroundingDINO → SAM2
   - Raycast detections into world space via `ViewportRaycaster`
   - Pin labeling and semantic normalization via `SemanticLabelMapper`

3. Meta scene understanding capture — **done**
   - Scene anchors + semantic labels via `MetaSceneCollector`
   - Plane/volume metadata

4. glTF export + sync — **done**
   - Export glTF + bin via `SpatialMapExporter`
   - Extras contain `cv_mapping` and `meta_scene`
   - Upload to `/v1/gltf` via `GltfSyncClient`

5. XR controller UI — **done**
   - Physical button mapping: A=Capture, B=CycleModel, X=PreviewDistance, Y=Export, Triggers=DisplayMode
   - Coloured circle indicators on controller models
   - Wrist HUD panel showing model/display/preview state
   - OVRInput polling via `XRControllerInput` → `ControllerMenuUI`
   - 3D spatial labels persist across captures

6. QA tests — **done**
   - 9 play-mode integration tests across 14 fixture images
   - CV server smoke tests via pytest

6.1 Web client — **done**
   - Vite/TypeScript WebXR client in `web/`
   - Desktop fallback workflow for non-WebXR browsers
   - Playwright browser coverage for capture/export UI flows

6.2 Scene mesh export validation — **in progress**
   - Verify `OVRTriangleMesh` data is captured and exported into glTF
   - Confirm `meta_scene.anchors[].has_mesh` and `mesh_*_count` are non-zero
   - Validate scene mesh nodes exist in exported glTF

7. End-to-end test on Quest 3 — **in progress**
   - Capture → pin → export → upload
   - Verify glTF extras on server
   - Confirm pins align to scene anchors for common classes

8. Capture accuracy improvements — **in progress**
   - CaptureFrame metadata (pose + intrinsics) for every snapshot
   - Intrinsics-based raycasting for bbox centers
   - Temporal smoothing + de-duplication for stable pins
   - Placement confidence based on raycast hit type

## Component Map

| Component | File | Role |
|-----------|------|------|
| `XRControllerInput` | `UI/XRControllerInput.cs` | Polls OVRInput, dispatches to ControllerMenuUI |
| `ControllerMenuUI` | `UI/ControllerMenuUI.cs` | State machine: model preset, display mode, preview distance |
| `ControllerMenuBuilder` | `UI/ControllerMenuBuilder.cs` | Builds HUD + button indicators at runtime |
| `SpatialCapturePipeline` | `Mapping/SpatialCapturePipeline.cs` | Orchestrates both capture pipelines |
| `SpatialPinManager` | `Mapping/SpatialPinManager.cs` | Unified pin storage + prefab spawning |
| `ViewportRaycaster` | `Mapping/ViewportRaycaster.cs` | 2D UV → 3D world hit (AR + Physics) |
| `LabelRenderer` | `Rendering/LabelRenderer.cs` | 3D label spawning (persistent mode) |
| `BBoxRenderer` | `Rendering/BBoxRenderer.cs` | 2D bbox overlay rendering |
| `SpatialMapExporter` | `Export/SpatialMapExporter.cs` | glTF export with merged extras |
| `QuestBuilder` | `Editor/QuestBuilder.cs` | One-click Quest APK build |

## Risks

- Pose drift during capture if the user moves between frame and raycast.
- Scene model availability depends on Space Setup and permission grants.
- Network latency can cause inconsistent pin placement.
- Accumulated labels may overlap in dense environments.

## Validation

- Use a fixed prompt set and known environment to compare CV vs Meta labels.
- Inspect `extras` in the uploaded glTF to verify schema and counts.
- Confirm pins align to scene anchors for common classes (floor, wall, couch, table).
- Walk around and press A multiple times — verify labels persist and accumulate.
