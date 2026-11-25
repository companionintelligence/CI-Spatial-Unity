using System;
using System.Collections.Generic;
using CompanionScan.Mapping;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CompanionScan.Replay
{
    /// <summary>One recorded camera frame: the image and where and how it was taken.</summary>
    [Serializable]
    public class ReplayFrame
    {
        public string id;

        /// <summary>When the frame was taken, in seconds on the recording's own clock.</summary>
        public double timestamp;

        public byte[] jpeg;
        public int width;
        public int height;

        /// <summary>The camera's pose in world space.</summary>
        public Pose pose = Pose.identity;

        /// <summary>Pixel intrinsics (fx m00, fy m11, cx m02, cy m12).</summary>
        public Matrix4x4 intrinsics;
    }

    /// <summary>
    /// A plane of the recorded room, for raycasts: the point it passes through and the normal that
    /// faces into the room. Planes are infinite, which is exact for the walls, floor and ceiling of a
    /// convex room seen from inside.
    /// </summary>
    [Serializable]
    public class ReplayPlane
    {
        public string label;
        public Vector3 point;
        public Vector3 normal = Vector3.up;
    }

    /// <summary>
    /// A recorded scan session: camera frames with their poses, the room's planes for raycasts and the
    /// scene snapshot a Space Setup read would give. The replay backend plays one back so the whole
    /// pipeline runs without a headset.
    /// </summary>
    [Serializable]
    public class ScanRecording
    {
        public const int FormatVersion = 1;

        public string name = "recording";
        public List<ReplayFrame> frames = new List<ReplayFrame>();
        public List<ReplayPlane> planes = new List<ReplayPlane>();
        public MetaSceneSnapshot scene = new MetaSceneSnapshot();

        /// <summary>JSON for the recording; the JPEGs are base64. Read it back with <see cref="FromJson"/>.</summary>
        public string ToJson()
        {
            var root = new JObject
            {
                ["version"] = FormatVersion,
                ["name"] = name,
                ["frames"] = new JArray(frames.ConvertAll(FrameToJson)),
                ["planes"] = new JArray(planes.ConvertAll(PlaneToJson)),
                ["scene"] = SceneToJson(scene)
            };
            return root.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>Reads what <see cref="ToJson"/> wrote. Throws when the text is not a recording of this format.</summary>
        public static ScanRecording FromJson(string json)
        {
            var root = JObject.Parse(json);
            int version = (int?)root["version"] ?? 0;
            if (version != FormatVersion)
            {
                throw new FormatException($"Scan recording version {version} is not supported (expected {FormatVersion}).");
            }

            var recording = new ScanRecording { name = (string)root["name"] ?? "recording" };
            foreach (var token in (JArray)root["frames"] ?? new JArray())
            {
                recording.frames.Add(FrameFromJson((JObject)token));
            }

            foreach (var token in (JArray)root["planes"] ?? new JArray())
            {
                recording.planes.Add(PlaneFromJson((JObject)token));
            }

            recording.scene = SceneFromJson((JObject)root["scene"]);
            return recording;
        }

        private static JObject FrameToJson(ReplayFrame frame)
        {
            return new JObject
            {
                ["id"] = frame.id,
                ["timestamp"] = frame.timestamp,
                ["width"] = frame.width,
                ["height"] = frame.height,
                ["jpeg_base64"] = frame.jpeg != null ? Convert.ToBase64String(frame.jpeg) : null,
                ["position"] = Array3(frame.pose.position),
                ["rotation"] = new JArray(frame.pose.rotation.x, frame.pose.rotation.y, frame.pose.rotation.z, frame.pose.rotation.w),
                ["intrinsics"] = new JObject
                {
                    ["fx"] = frame.intrinsics.m00,
                    ["fy"] = frame.intrinsics.m11,
                    ["cx"] = frame.intrinsics.m02,
                    ["cy"] = frame.intrinsics.m12
                }
            };
        }

        private static ReplayFrame FrameFromJson(JObject token)
        {
            var intrinsics = Matrix4x4.zero;
            var k = (JObject)token["intrinsics"];
            if (k != null)
            {
                intrinsics.m00 = (float?)k["fx"] ?? 0f;
                intrinsics.m11 = (float?)k["fy"] ?? 0f;
                intrinsics.m02 = (float?)k["cx"] ?? 0f;
                intrinsics.m12 = (float?)k["cy"] ?? 0f;
                intrinsics.m22 = 1f;
            }

            var rotation = (JArray)token["rotation"];
            var base64 = (string)token["jpeg_base64"];
            return new ReplayFrame
            {
                id = (string)token["id"],
                timestamp = (double?)token["timestamp"] ?? 0.0,
                width = (int?)token["width"] ?? 0,
                height = (int?)token["height"] ?? 0,
                jpeg = string.IsNullOrEmpty(base64) ? null : Convert.FromBase64String(base64),
                pose = new Pose(Vector3From((JArray)token["position"]),
                    rotation == null ? Quaternion.identity : new Quaternion((float)rotation[0], (float)rotation[1], (float)rotation[2], (float)rotation[3])),
                intrinsics = intrinsics
            };
        }

        private static JObject PlaneToJson(ReplayPlane plane)
        {
            return new JObject { ["label"] = plane.label, ["point"] = Array3(plane.point), ["normal"] = Array3(plane.normal) };
        }

        private static ReplayPlane PlaneFromJson(JObject token)
        {
            return new ReplayPlane
            {
                label = (string)token["label"],
                point = Vector3From((JArray)token["point"]),
                normal = Vector3From((JArray)token["normal"])
            };
        }

        private static JObject SceneToJson(MetaSceneSnapshot scene)
        {
            var anchors = new JArray();
            foreach (var anchor in scene?.anchors ?? new List<MetaSceneAnchorSnapshot>())
            {
                anchors.Add(new JObject
                {
                    ["uuid"] = anchor.uuid,
                    ["labels"] = new JArray(anchor.labels ?? new List<string>()),
                    ["position"] = Array3(anchor.position),
                    ["rotation"] = new JArray(anchor.rotation.x, anchor.rotation.y, anchor.rotation.z, anchor.rotation.w),
                    ["has_plane"] = anchor.hasPlane,
                    ["plane_size"] = Array3(anchor.planeSize),
                    ["has_volume"] = anchor.hasVolume,
                    ["volume_size"] = Array3(anchor.volumeSize)
                });
            }

            return new JObject
            {
                ["room_uuids"] = new JArray(scene?.roomUuids ?? new List<string>()),
                ["primary_room_uuid"] = scene?.primaryRoomUuid,
                ["anchors"] = anchors
            };
        }

        private static MetaSceneSnapshot SceneFromJson(JObject token)
        {
            var scene = new MetaSceneSnapshot();
            if (token == null)
            {
                return scene;
            }

            scene.primaryRoomUuid = (string)token["primary_room_uuid"];
            foreach (var uuid in (JArray)token["room_uuids"] ?? new JArray())
            {
                scene.roomUuids.Add((string)uuid);
            }

            foreach (JObject a in (JArray)token["anchors"] ?? new JArray())
            {
                var rotation = (JArray)a["rotation"];
                var anchor = new MetaSceneAnchorSnapshot
                {
                    uuid = (string)a["uuid"],
                    position = Vector3From((JArray)a["position"]),
                    rotation = rotation == null ? Quaternion.identity : new Quaternion((float)rotation[0], (float)rotation[1], (float)rotation[2], (float)rotation[3]),
                    hasPlane = (bool?)a["has_plane"] ?? false,
                    planeSize = Vector3From((JArray)a["plane_size"]),
                    hasVolume = (bool?)a["has_volume"] ?? false,
                    volumeSize = Vector3From((JArray)a["volume_size"])
                };
                foreach (var label in (JArray)a["labels"] ?? new JArray())
                {
                    anchor.labels.Add((string)label);
                }

                scene.anchors.Add(anchor);
            }

            return scene;
        }

        private static JArray Array3(Vector3 v) => new JArray(v.x, v.y, v.z);

        private static Vector3 Vector3From(JArray array)
        {
            return array == null ? Vector3.zero : new Vector3((float)array[0], (float)array[1], (float)array[2]);
        }
    }
}
