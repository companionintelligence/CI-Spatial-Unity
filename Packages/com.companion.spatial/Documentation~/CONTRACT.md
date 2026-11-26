# Scan extras v2: what changed from v1 and from the web contract

`scan-extras.v2.schema.json` (next to this file) is the schema. The v2 document is the object that goes into the
root `extras` of a glTF the Unity scanner exports. `ExtrasReader` reads v1 and v2 files into one model,
`ExtrasWriter` writes v2, `ExtrasMerge` merges a new export into an earlier file by object id.

Names follow the web Scan contract (`CI-WebXR-Scan-Meta/contracts/schemas`) where the concept is the same. This table
lists every place where v2 differs from the Unity v1 extras, and says whether v2 follows the web contract or deviates
from it, and why.

| Field | v1 key | v2 key | Why |
|---|---|---|---|
| Schema marker | `schema_version: "1.0"` (text) | `schema: "companion.scan-extras"` and `schema_version: 2` (number) | A v2 file names its schema. The web has no equivalent (its `contract_version` is the server API's). |
| Space id | `space_id`, twice (top level and `cv_mapping`) | `space.id` | One place. |
| Export time | `timestamp_utc` (a re-export kept the old one) | `space.updated_at_utc`, plus `space.created_at_utc` | Both ends of the file's life. Web: `updated_at` and `created_at`; the `_utc` suffix says the zone, as in `agreed_at_utc`. |
| Coordinate frame | none (Unity world; the v1 docs call it both left- and right-handed) | `space.frame`: `unity-world` or `local-floor` | Web poses are WebXR `local-floor`, right-handed; Unity's are left-handed. Said once per file, not per pose (web: `Pose.frame`, a constant). A merge refuses to mix two frames. |
| Pins | `pins` and `cv_mapping.pins`, plus a copy at the top level | `objects` | One list. Web: detections. |
| Object id | none | `objects[].id` | Unique in the file, stable across exports: the merge key. v1 pins get `<session>-track-<n>`, `anchor-<uuid>` or `pin-<index>`. |
| Track | `track_id` | `track_id` (integer) | Same. The web Quest app's glTF extras write it as text ("1-0"); the reader accepts numeric text. |
| Label | `label`, `semantic_label` | `label { text, tag, semantic }` | The tag vocabulary: raw text, canonical tag, one of Meta's 16 labels. Web: `label` is the raw text. |
| Score | `score`, `placement_confidence` | `confidence` (fused over the track), `observations[].score` (per detection) | Web: `confidence` is per detection, so it matches the observation's `score`. |
| Source | `CvServer` or `MetaScene` | `cv`, `device` or `manual` | A hand-placed object is a third kind. |
| Pose | `position [x,y,z]`, `rotation [x,y,z,w]` | `pose { position {x,y,z}, quaternion {x,y,z,w} }` | Web `Pose`, without its `frame`. The reader also takes arrays and the name `rotation`. |
| 2D box | `bbox_normalized [x,y,w,h]` from the top left, `bbox_pixels` | `observations[].bbox_cxcywh [cx,cy,w,h]` | Web name and format. Boxes are fractions of the image only: no pixel-or-fraction guess. |
| Capture | `capture_id`, `capture_session_id`, `capture_frame_index` | `observations[].frame_id` | The session goes into the object id; the rest stays under `v1`. |
| Extent | `volume_size` | `objects[].bounds { center, size }` | A box around the object, in world space. |
| Snapshots | `snapshots`, `cv_mapping.snapshots` | `frames` | Web: keyframes. |
| Camera | `camera { position, rotation }`, `intrinsics` (16 numbers), `image_size` | `frames[].pose`, `frames[].intrinsics { fx, fy, cx, cy, width, height }` | Web: `{fx,fy,cx,cy}` with the size in `image`; here the size sits beside them. v1's `fx` is `m00`, `fy` `m11`, `cx` `m02`, `cy` `m12`. |
| Frame time | `timestamp` (scan clock) | `frames[].timestamp_utc` | v1 only had the scan clock, so an upgraded frame has none; the clock value stays under `v1`. |
| Scene | `meta_scene.anchors[]` | `planes[]` | Web: planes. Upgraded labels: `label` is the tag, `semantic` the Meta label. |
| Plane size | `plane_center`, `plane_size` as `[x,y,0]` | `center {x,y}`, `size {x,y}` | **Deviation.** Web: `{x,z}` with the normal along +y. Meta anchors are x/y rectangles, so v2 keeps their own axes (those of `pose`); the web exporter has to convert. |
| Plane outline | `plane_boundary` (world space) | `boundary` | Same data; always a list. |
| Consent | none | `consent { notice_version, agreed_at_utc, server }` | **Deviation.** Web: `{camera, granted_at, recorded_at}`, a camera opt-in. v2 records which notice was agreed to and where the data may go. |
| Device | none | `device { model, os, app, app_version }` | Web: `client { name, version, device, session_id }`. |
| Bounds | `bounds { center [..], size [..] }` | `bounds { center {..}, size {..} }` | Recomputed on every merge. |
| Empty lists | `"pins": null` after a merge | `[]` | A list is always an array. |

## What v2 has no place for

Nothing that carries information is dropped when a v1 file is read. A v1 member that v2 does not model stays, as it
was, under the key `"v1"` of the part it belonged to: on an object `hit_type`, `prompt`, `placement_confidence`, `plane_size`,
`capture_session_id`, `capture_frame_index`, `timestamp`; on a plane `has_volume`, `volume_size`, `has_mesh` and
the mesh counts; on a frame `timestamp` and `frustum`; at the top level `boundary`, `floorplan` and the notes of
`meta_scene` (`available`, `error`, `room_uuids`, ...). A pin or an anchor without a position cannot be placed: it stays,
whole, under `v1.skipped_pins` or `v1.skipped_anchors`, with a warning. A pin listed twice (the old merge did that)
becomes one object. A top-level key of a v1 file that has the name of a v2 part (`consent`, `device`, `objects`, ...) is
kept under `v1` as well, so a v2 reader does not take it for that part. Null, empty text, empty lists and empty objects are not kept.

Keys the reader does not know at all, in v1 or v2 and at any level, are kept in the part's `Unknown` bag and written
back after the known keys.
