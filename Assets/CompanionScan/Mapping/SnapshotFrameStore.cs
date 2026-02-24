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

        public void AddSnapshot(byte[] jpegBytes, int width, int height, Pose cameraPose,
                                Matrix4x4 intrinsics, float frustumDistance, Camera camera)
        {
            if (jpegBytes == null || jpegBytes.Length == 0)
            {
                return;
            }

            var frame = new SnapshotFrame
            {
                id = BuildId(),
                timestamp = Time.timeAsDouble,
                jpegBytes = jpegBytes,
                imageWidth = width,
                imageHeight = height,
                cameraPosition = cameraPose.position,
                cameraRotation = cameraPose.rotation,
                frustumDistance = frustumDistance,
                cameraIntrinsics = intrinsics,
                frustumCorners = BuildFrustumCorners(camera, frustumDistance)
            };

            _frames.Add(frame);

            if (_frames.Count > Mathf.Max(1, maxFrames))
            {
                _frames.RemoveAt(0);
            }
        }

        private string BuildId()
        {
            _counter++;
            return $"{idPrefix}_{_counter:0000}";
        }

        private static Vector3[] BuildFrustumCorners(Camera cam, float distance)
        {
            if (cam == null || distance <= 0f)
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
