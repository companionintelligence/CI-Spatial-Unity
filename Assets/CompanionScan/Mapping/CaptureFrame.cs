using System;
using UnityEngine;

namespace CompanionScan.Mapping
{
    [Serializable]
    public class CaptureFrame
    {
        public string id;
        public string sessionId;
        public int frameIndex;
        public double timestamp;
        public byte[] jpegBytes;
        public int imageWidth;
        public int imageHeight;
        public Pose cameraPose;
        public Matrix4x4 cameraIntrinsics;

        /// <summary>
        /// A focal length at or below this (pixels) is not a real camera: the identity matrix, which
        /// stands in for "no intrinsics" in some callers, has one pixel, and a normalised matrix has
        /// a value near one. Either would give every pixel a nearly sideways ray.
        /// </summary>
        public const float MinFocalLengthPixels = 1f;

        /// <summary>
        /// True when the matrix holds pixel intrinsics: both focal lengths (m00 and m11) are finite
        /// and above <see cref="MinFocalLengthPixels"/>. The zero matrix means "unknown".
        /// </summary>
        public bool HasIntrinsics
        {
            get
            {
                return IsPixelFocalLength(cameraIntrinsics.m00) && IsPixelFocalLength(cameraIntrinsics.m11);
            }
        }

        private static bool IsPixelFocalLength(float focalLength)
        {
            return focalLength > MinFocalLengthPixels && !float.IsInfinity(focalLength);
        }

        public static CaptureFrame Create(byte[] bytes, int width, int height, Pose pose, Matrix4x4 intrinsics)
        {
            return new CaptureFrame
            {
                timestamp = Time.timeAsDouble,
                jpegBytes = bytes,
                imageWidth = width,
                imageHeight = height,
                cameraPose = pose,
                cameraIntrinsics = intrinsics
            };
        }
    }
}
