# Current Plan

## Goal
Build a Quest 3 local world/space mapper that captures CV detections and Meta scene anchors, exports a glTF with unified extras, and syncs it back to the local server.

## Milestones
1) Local CV server stable
   - Docker build + GPU inference
   - Endpoints: detect, segment, florence, gltf upload

2) Quest capture + pinning
   - Camera frame capture
   - Raycast detections into world space
   - Pin labeling and semantic normalization

3) Meta scene understanding capture
   - Scene anchors + semantic labels
   - Plane/volume metadata

4) glTF export + sync
   - Export glTF + bin
   - Extras contain cv_mapping and meta_scene
   - Upload to `/v1/gltf`

5) End-to-end test on Quest 3
   - Capture -> pin -> export -> upload
   - Verify glTF extras on server

## Risks
- Pose drift during capture if the user moves between frame and raycast.
- Scene model availability depends on Space Setup and permission grants.
- Network latency can cause inconsistent pin placement.

## Validation
- Use a fixed prompt set and known environment to compare CV vs Meta labels.
- Inspect `extras` in the uploaded glTF to verify schema and counts.
- Confirm pins align to scene anchors for common classes (floor, wall, couch, table).
