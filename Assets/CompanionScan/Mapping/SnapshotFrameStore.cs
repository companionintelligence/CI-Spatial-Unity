using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompanionScan.Mapping
{
    [Serializable]
    public class SnapshotFrame
    {
        public string id;
        public double timestamp;
        public byte[] jpegBytes;
        public int imageWidth;
        public int imageHeight;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public float frustumDistance;
        public Vector3[] frustumCorners;
        public Matrix4x4 cameraIntrinsics;
    }

    /// <summary>
    /// Stores captured snapshot images and frustum metadata for later export.
    /// </summary>
    public class SnapshotFrameStore : MonoBehaviour
    {
        [SerializeField] private int maxFrames = 50;
        [SerializeField] private string idPrefix = "snap";

        private readonly List<SnapshotFrame> _frames = new List<SnapshotFrame>();
        private int _counter;

        public IReadOnlyList<SnapshotFrame> Frames => _frames;

        public void Clear()
        {
            _frames.Clear();
        }

        public List<SnapshotFrame> ConsumeFrames()
        {
            var copy = new List<SnapshotFrame>(_frames);
            _frames.Clear();
            return copy;
        }

        public string AddSnapshot(CaptureFrame capture, float frustumDistance, Camera camera)
        {
            if (capture == null || capture.jpegBytes == null || capture.jpegBytes.Length == 0)
            {
                return null;
            }

            var frame = new SnapshotFrame
            {
                id = BuildId(),
                timestamp = capture.timestamp > 0 ? capture.timestamp : Time.timeAsDouble,
                jpegBytes = capture.jpegBytes,
                imageWidth = capture.imageWidth,
                imageHeight = capture.imageHeight,
                cameraPosition = capture.cameraPose.position,
                cameraRotation = capture.cameraPose.rotation,
                frustumDistance = frustumDistance,
                cameraIntrinsics = capture.cameraIntrinsics,
                frustumCorners = BuildFrustumCorners(capture, camera, frustumDistance)
            };

            _frames.Add(frame);

            if (_frames.Count > Mathf.Max(1, maxFrames))
            {
                // A pin found in the dropped snapshot still names it, and the export will not have it.
                Debug.LogWarning($"[Snapshots] Dropping {_frames[0].id}: the store keeps the newest {Mathf.Max(1, maxFrames)} snapshots. " +
                                 "Pins found in it name a snapshot the export will not have; export before the limit is reached.");
                _frames.RemoveAt(0);
            }

            return frame.id;
        }

        private string BuildId()
        {
            _counter++;
            return $"{idPrefix}_{_counter:0000}";
        }

        /// <summary>
        /// The four corners of the view at <paramref name="distance"/> in front of the camera, in the
        /// order bottom left, top left, top right, bottom right. A capture with intrinsics and an image
        /// size supplies its own view (its pose, focal lengths and principal point), so a capture taken
        /// elsewhere, such as a replayed one, is not drawn from the scene camera's point of view. Without
        /// them, the scene camera is all there is.
        /// </summary>
        private static Vector3[] BuildFrustumCorners(CaptureFrame capture, Camera cam, float distance)
        {
            if (distance <= 0f)
            {
                return Array.Empty<Vector3>();
            }

            if (capture.HasIntrinsics && capture.imageWidth > 0 && capture.imageHeight > 0)
            {
                float fx = capture.cameraIntrinsics.m00;
                float fy = capture.cameraIntrinsics.m11;
                float cx = capture.cameraIntrinsics.m02;
                float cy = capture.cameraIntrinsics.m12;
                float width = capture.imageWidth;
                float height = capture.imageHeight;

                // Pixel rows count down from the top; camera space has Y up.
                Vector3 Corner(float px, float py) => capture.cameraPose.position
                    + capture.cameraPose.rotation * new Vector3((px - cx) / fx * distance, -(py - cy) / fy * distance, distance);

                return new[] { Corner(0f, height), Corner(0f, 0f), Corner(width, 0f), Corner(width, height) };
            }

            if (cam == null)
            {
                return Array.Empty<Vector3>();
            }

            return new[]
            {
                cam.ViewportToWorldPoint(new Vector3(0f, 0f, distance)),
                cam.ViewportToWorldPoint(new Vector3(0f, 1f, distance)),
                cam.ViewportToWorldPoint(new Vector3(1f, 1f, distance)),
                cam.ViewportToWorldPoint(new Vector3(1f, 0f, distance))
            };
        }
    }
}
