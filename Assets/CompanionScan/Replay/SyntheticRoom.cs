using System.Collections.Generic;
using CompanionScan.Mapping;
using UnityEngine;

namespace CompanionScan.Replay
{
    /// <summary>
    /// Makes a recording of a room that never existed, so tests and demos need no headset and no image
    /// files: a box room 4 m wide (x from -2 to 2), 6 m long (z from -3 to 3) and 2.6 m high, and camera
    /// frames at eye height that look along known directions. Where a pixel's ray meets the walls is then
    /// plain geometry a test can work out by hand.
    /// </summary>
    public static class SyntheticRoom
    {
        public const int ImageWidth = 640;
        public const int ImageHeight = 480;
        public const float FocalLengthPixels = 500f;
        public const float EyeHeight = 1.5f;
        public const float HalfWidth = 2f;
        public const float HalfLength = 3f;
        public const float Height = 2.6f;

        /// <summary>
        /// A room with one frame per heading, taken from the middle of the room at eye height, one second
        /// apart from time 10. Heading 0 looks along +z, 90 along +x, 180 along -z, 270 along -x.
        /// </summary>
        public static ScanRecording Build(params float[] headingsDegrees)
        {
            if (headingsDegrees == null || headingsDegrees.Length == 0)
            {
                headingsDegrees = new[] { 0f, 90f };
            }

            var recording = new ScanRecording { name = "synthetic-room" };
            var jpeg = MakeJpeg();
            for (int i = 0; i < headingsDegrees.Length; i++)
            {
                recording.frames.Add(new ReplayFrame
                {
                    id = $"frame{i}",
                    timestamp = 10.0 + i,
                    jpeg = jpeg,
                    width = ImageWidth,
                    height = ImageHeight,
                    pose = new Pose(new Vector3(0f, EyeHeight, 0f), Quaternion.Euler(0f, headingsDegrees[i], 0f)),
                    intrinsics = Intrinsics()
                });
            }

            AddPlane(recording, "FLOOR", Vector3.zero, Vector3.up, new Vector3(2f * HalfWidth, 2f * HalfLength, 0f));
            AddPlane(recording, "CEILING", new Vector3(0f, Height, 0f), Vector3.down, new Vector3(2f * HalfWidth, 2f * HalfLength, 0f));
            AddPlane(recording, "WALL_FACE", new Vector3(-HalfWidth, Height / 2f, 0f), Vector3.right, new Vector3(2f * HalfLength, Height, 0f));
            AddPlane(recording, "WALL_FACE", new Vector3(HalfWidth, Height / 2f, 0f), Vector3.left, new Vector3(2f * HalfLength, Height, 0f));
            AddPlane(recording, "WALL_FACE", new Vector3(0f, Height / 2f, -HalfLength), Vector3.forward, new Vector3(2f * HalfWidth, Height, 0f));
            AddPlane(recording, "WALL_FACE", new Vector3(0f, Height / 2f, HalfLength), Vector3.back, new Vector3(2f * HalfWidth, Height, 0f));

            recording.scene.roomUuids.Add("synthetic-room");
            recording.scene.primaryRoomUuid = "synthetic-room";
            recording.scene.supportedLabels.AddRange(SemanticLabelMapper.SupportedMetaLabels);
            return recording;
        }

        /// <summary>The intrinsics of every synthetic frame: a 500 pixel focal length and the principal point in the middle of 640 x 480.</summary>
        public static Matrix4x4 Intrinsics()
        {
            var matrix = Matrix4x4.zero;
            matrix.m00 = FocalLengthPixels;
            matrix.m11 = FocalLengthPixels;
            matrix.m02 = ImageWidth / 2f;
            matrix.m12 = ImageHeight / 2f;
            matrix.m22 = 1f;
            return matrix;
        }

        /// <summary>A small valid JPEG of the right size (a gradient), made with the engine's encoder.</summary>
        public static byte[] MakeJpeg()
        {
            var texture = new Texture2D(ImageWidth, ImageHeight, TextureFormat.RGB24, false);
            try
            {
                var pixels = new Color32[ImageWidth * ImageHeight];
                for (int y = 0; y < ImageHeight; y++)
                {
                    for (int x = 0; x < ImageWidth; x++)
                    {
                        pixels[y * ImageWidth + x] = new Color32((byte)(x * 255 / ImageWidth), (byte)(y * 255 / ImageHeight), 128, 255);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                return texture.EncodeToJPG(75);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void AddPlane(ScanRecording recording, string label, Vector3 point, Vector3 normal, Vector3 size)
        {
            recording.planes.Add(new ReplayPlane { label = label, point = point, normal = normal });
            recording.scene.anchors.Add(new MetaSceneAnchorSnapshot
            {
                uuid = $"{label.ToLowerInvariant()}-{recording.scene.anchors.Count}",
                labels = new List<string> { label },
                position = point,
                rotation = Quaternion.FromToRotation(Vector3.up, normal),
                hasPlane = true,
                planeSize = size
            });
        }
    }
}
