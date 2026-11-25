// Models and documents the extras tests build on.

using System.Collections.Generic;
using Companion.Spatial.Extras;
using Companion.Spatial.Vocabulary;
using Newtonsoft.Json.Linq;

namespace Companion.Spatial.Tests
{
    internal static class Models
    {
        internal const string Created = "2026-11-25T09:00:00.0000000Z";
        internal const string Updated = "2026-11-25T10:00:00.0000000Z";

        internal static ScanPose Pose(double x, double y, double z)
        {
            return new ScanPose(new Vec3(x, y, z), Quat.Identity);
        }

        internal static ScanObject CvObject(string id, int track, string text, double x, double y, double z, double confidence = 0.75, string frame = null)
        {
            var obj = new ScanObject
            {
                Id = id,
                TrackId = track,
                Label = ScanLabel.FromText(text),
                Confidence = confidence,
                Source = ObjectSource.Cv,
                Pose = Pose(x, y, z),
                Normal = new Vec3(0, 1, 0)
            };
            if (frame != null)
            {
                NormalizedBox.TryCreate(0.1, 0.2, 0.5, 0.8, out var box);
                obj.Observations.Add(new ScanObservation { FrameId = frame, Box = box, Score = confidence });
            }

            return obj;
        }

        internal static ScanObject DeviceObject(string anchor, string label, double x, double y, double z)
        {
            return new ScanObject
            {
                Id = ScanObject.MakeAnchorId(anchor),
                Label = ScanLabel.FromText(label),
                Confidence = 1.0,
                Source = ObjectSource.Device,
                Pose = Pose(x, y, z),
                AnchorUuid = anchor
            };
        }

        internal static ScanFrame Frame(string id, double x, double y, double z)
        {
            return new ScanFrame
            {
                Id = id,
                TimestampUtc = "2026-11-25T09:30:00.0000000Z",
                Image = "snapshots/" + id + ".jpg",
                Pose = new ScanPose(new Vec3(x, y, z), new Quat(0.125, 0.25, 0.375, 0.5)),
                Intrinsics = new ScanIntrinsics { Fx = 600, Fy = 600, Cx = 320, Cy = 240, Width = 640, Height = 480 }
            };
        }

        internal static ScanPlane Wall(string id)
        {
            var plane = new ScanPlane
            {
                Id = id,
                Label = "wall",
                Semantic = "WALL_FACE",
                Pose = Pose(2, 1.25, 0),
                Center = new Vec2(0, 0),
                Size = new Vec2(4, 2.5)
            };
            plane.Boundary.Add(new Vec3(0, 0, 0));
            plane.Boundary.Add(new Vec3(4, 0, 0));
            plane.Boundary.Add(new Vec3(4, 2.5, 0));
            plane.Boundary.Add(new Vec3(0, 2.5, 0));
            return plane;
        }

        /// <summary>A document with every part filled in: two objects, a frame, a plane, consent, device and bounds.</summary>
        internal static ScanExtras Full()
        {
            var extras = new ScanExtras();
            extras.Space.Id = "room-1";
            extras.Space.CreatedAtUtc = Created;
            extras.Space.UpdatedAtUtc = Updated;
            extras.Consent = new ScanConsent { NoticeVersion = "2026-11", AgreedAtUtc = "2026-11-25T08:55:00.0000000Z", Server = "https://scan.example.test" };
            extras.Device = new ScanDevice { Model = "Meta Quest 3", Os = "Horizon OS 78", App = "companion-scan", AppVersion = "1.4.0" };
            extras.Frames.Add(Frame("snap_0001", 0.5, 1.5, -1));
            extras.Objects.Add(CvObject("s1-track-1", 1, "TV screen", 1, 0.5, 2, 0.75, "snap_0001"));
            var table = DeviceObject("a1", "TABLE", 1, 0.75, 3.5);
            table.Bounds = new ScanBounds(new Vec3(1, 0.75, 3.5), new Vec3(1.5, 0.75, 0.6));
            extras.Objects.Add(table);
            extras.Planes.Add(Wall("a2"));

            // the box around the objects and the wall: x 0 to 4, y 0 to 2.5, z 0 to 3.8 (the table ends at 3.8)
            extras.Bounds = new ScanBounds(new Vec3(2, 1.25, 1.9), new Vec3(4, 2.5, 3.8));
            return extras;
        }

        /// <summary>The documents the schema must accept: the writer's output for each.</summary>
        internal static IEnumerable<KeyValuePair<string, ScanExtras>> ValidModels()
        {
            yield return new KeyValuePair<string, ScanExtras>("empty", new ScanExtras());
            yield return new KeyValuePair<string, ScanExtras>("full", Full());

            var minimal = new ScanExtras();
            minimal.Objects.Add(new ScanObject { Id = "o1" });
            yield return new KeyValuePair<string, ScanExtras>("one bare object", minimal);

            var framesOnly = new ScanExtras();
            framesOnly.Frames.Add(new ScanFrame { Id = "f1" });
            yield return new KeyValuePair<string, ScanExtras>("one bare frame", framesOnly);
        }

        /// <summary>A complete v2 document, written by hand (not by the writer), with an unknown key at every level.</summary>
        internal const string V2Document = @"{
            'schema': 'companion.scan-extras', 'schema_version': 2, 'units': 'meters',
            'future_root_key': { 'a': [ 1, 2 ] },
            'space': { 'id': 'room-9', 'frame': 'unity-world', 'created_at_utc': '2026-11-24T08:00:00Z', 'updated_at_utc': '2026-11-25T10:00:00Z', 'future_space_key': 7 },
            'consent': { 'notice_version': '2026-11', 'agreed_at_utc': '2026-11-24T07:59:00Z', 'server': 'https://scan.example.test', 'future_consent_key': true },
            'device': { 'model': 'Meta Quest 3', 'os': 'Horizon OS 78', 'app': 'companion-scan', 'app_version': '1.4.0', 'future_device_key': 'x' },
            'frames': [ {
                'id': 'snap_0001', 'timestamp_utc': '2026-11-24T08:30:00Z', 'image': 'snapshots/snap_0001.jpg',
                'pose': { 'position': { 'x': 0.5, 'y': 1.5, 'z': -1.0 }, 'quaternion': { 'x': 0.125, 'y': 0.25, 'z': 0.375, 'w': 0.5 }, 'frame': 'local-floor' },
                'intrinsics': { 'fx': 600.0, 'fy': 601.0, 'cx': 320.0, 'cy': 240.0, 'width': 640, 'height': 480, 'distortion': [ 0.1 ] },
                'future_frame_key': 'f'
            } ],
            'objects': [ {
                'id': 's1-track-1', 'track_id': 1,
                'label': { 'text': 'TV screen', 'tag': 'screen', 'semantic': 'SCREEN', 'future_label_key': 1 },
                'confidence': 0.75, 'source': 'cv',
                'pose': { 'position': { 'x': 1.0, 'y': 0.5, 'z': 2.0 }, 'quaternion': { 'x': 0.0, 'y': 0.0, 'z': 0.0, 'w': 1.0 } },
                'normal': { 'x': 0.0, 'y': 1.0, 'z': 0.0 },
                'bounds': { 'center': { 'x': 1.0, 'y': 0.5, 'z': 2.0 }, 'size': { 'x': 1.0, 'y': 0.6, 'z': 0.1 } },
                'anchor_uuid': 'a1',
                'observations': [ { 'frame_id': 'snap_0001', 'bbox_cxcywh': [ 0.3, 0.5, 0.4, 0.6 ], 'score': 0.75, 'future_observation_key': 0 } ],
                'future_object_key': [ 1 ]
            } ],
            'planes': [ {
                'id': 'a2', 'label': 'wall', 'semantic': 'WALL_FACE',
                'pose': { 'position': { 'x': 2.0, 'y': 1.25, 'z': 0.0 }, 'quaternion': { 'x': 0.0, 'y': 0.0, 'z': 0.0, 'w': 1.0 } },
                'center': { 'x': 0.0, 'y': 0.0 }, 'size': { 'x': 4.0, 'y': 2.5 },
                'boundary': [ { 'x': 0.0, 'y': 0.0, 'z': 0.0 }, { 'x': 4.0, 'y': 0.0, 'z': 0.0 } ],
                'future_plane_key': null
            } ],
            'bounds': { 'center': { 'x': 2.0, 'y': 1.25, 'z': 1.0 }, 'size': { 'x': 4.0, 'y': 2.5, 'z': 2.0 }, 'future_bounds_key': 'b' }
        }";

        /// <summary>The golden v1 file of the scanner, as an object.</summary>
        internal static JObject GoldenV1()
        {
            return JsonAssert.ParseObject(TestPaths.ReadGoldenV1());
        }

        /// <summary>
        /// The golden file in the shape the exporter really writes: the five members of cv_mapping repeated at the top
        /// level, a Space Setup scene with one anchor, and a time. (The golden file lists them once and has no scene.)
        /// </summary>
        internal static JObject ExporterShapedV1()
        {
            var root = GoldenV1();
            root["timestamp_utc"] = "2026-11-25T10:15:30.1234567Z";
            var cv = (JObject)root["cv_mapping"];
            foreach (var key in new[] { "bounds", "boundary", "floorplan", "pins", "snapshots" })
            {
                root[key] = cv[key].DeepClone();
            }

            root["meta_scene"] = JsonAssert.Parse(@"{
                'available': true, 'error': null, 'timestamp': 3.5,
                'supported_labels': [ 'TABLE', 'OTHER' ], 'room_uuids': [ 'room-1' ], 'primary_room_uuid': 'room-1',
                'anchors': [ {
                    'uuid': 'anchor-1', 'labels': [ 'TABLE' ],
                    'position': [ 1, 0.5, 2 ], 'rotation': [ 0.125, 0.25, 0.375, 0.5 ],
                    'has_plane': true, 'plane_center': [ 1, 0.5, 0 ], 'plane_size': [ 1.5, 0.75, 0 ],
                    'plane_boundary': [ [ 0, 0, 0 ], [ 1, 0, 0 ], [ 1, 0, 1 ] ],
                    'has_volume': false, 'volume_center': [ 0, 0, 0 ], 'volume_size': [ 0, 0, 0 ],
                    'has_mesh': true, 'mesh_vertex_count': 100, 'mesh_triangle_count': 50
                } ]
            }");
            root["space_id"] = "room-1";
            return root;
        }
    }
}
