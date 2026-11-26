using UnityEngine;

namespace CompanionScan.Core
{
    /// <summary>
    /// One camera frame with the pose and the intrinsics of the moment it was taken. A source that
    /// reads them afterwards (the headset's pose a few frames later, the camera of another frame)
    /// puts every pin a little off, and further off the faster the user turns.
    /// </summary>
    public readonly struct CameraFrame
    {
        public CameraFrame(byte[] jpeg, int width, int height, double timestamp, Pose pose, Matrix4x4 intrinsics,
            bool poseIsGuaranteed = true)
        {
            Jpeg = jpeg;
            Width = width;
            Height = height;
            Timestamp = timestamp;
            Pose = pose;
            Intrinsics = intrinsics;
            PoseIsGuaranteed = poseIsGuaranteed;
        }

        /// <summary>The frame, JPEG encoded.</summary>
        public byte[] Jpeg { get; }

        /// <summary>Image width in pixels, or 0 when the source does not say (the pipeline then reads it from the JPEG).</summary>
        public int Width { get; }

        /// <summary>Image height in pixels, or 0 when the source does not say.</summary>
        public int Height { get; }

        /// <summary>When the frame was taken, in seconds on the <c>Time.timeAsDouble</c> clock.</summary>
        public double Timestamp { get; }

        /// <summary>The camera's pose in world space when the frame was taken.</summary>
        public Pose Pose { get; }

        /// <summary>Pixel intrinsics (fx in m00, fy in m11, cx in m02, cy in m12), or the zero matrix when unknown.</summary>
        public Matrix4x4 Intrinsics { get; }

        /// <summary>
        /// True when the source promises <see cref="Pose"/> is the camera's world pose at capture, which
        /// every <see cref="ICameraSource"/> does. It is false for a frame read from a plain
        /// <see cref="ICameraProvider"/>, whose pose is whatever its transform says now; the pipeline
        /// then checks it against the headset's own pose (<c>CapturePose</c>).
        /// </summary>
        public bool PoseIsGuaranteed { get; }
    }

    /// <summary>
    /// A camera backend: the Meta passthrough camera, ARKit and ARCore, a Camera2 plugin, a recorded
    /// session. It answers "the frame, and where and how it was taken" in one call, so the pose and the
    /// intrinsics belong to the frame.
    /// </summary>
    public interface ICameraSource : ICameraProvider
    {
        /// <summary>
        /// Takes the current frame. Returns false, with no frame, when the camera has nothing yet
        /// (not started, no permission, no new image).
        /// </summary>
        bool TryCaptureFrame(int jpegQuality, out CameraFrame frame);
    }

    /// <summary>Finds or makes the <see cref="ICameraSource"/> for a camera provider.</summary>
    public static class CameraSources
    {
        /// <summary>
        /// The provider itself when it is already an <see cref="ICameraSource"/>, otherwise an adapter
        /// that reads the frame, the pose and the intrinsics from it one after the other. Null for null.
        /// </summary>
        public static ICameraSource From(ICameraProvider provider)
        {
            if (provider == null)
            {
                return null;
            }

            return provider as ICameraSource ?? new LegacyCameraSource(provider);
        }
    }

    /// <summary>
    /// Adapts an <see cref="ICameraProvider"/> that predates <see cref="ICameraSource"/>: the three
    /// reads happen back to back in one call, with the clock as the timestamp, and the pose is marked as
    /// not guaranteed.
    /// </summary>
    public sealed class LegacyCameraSource : ICameraSource
    {
        private readonly ICameraProvider _provider;

        public LegacyCameraSource(ICameraProvider provider)
        {
            _provider = provider;
        }

        public bool IsAvailable => _provider.IsAvailable;

        public void StartCapture() => _provider.StartCapture();

        public void StopCapture() => _provider.StopCapture();

        public byte[] GetCurrentFrameJpeg(int quality = 75) => _provider.GetCurrentFrameJpeg(quality);

        public Matrix4x4 GetCameraIntrinsics() => _provider.GetCameraIntrinsics();

        public Pose GetCameraPose() => _provider.GetCameraPose();

        public bool TryCaptureFrame(int jpegQuality, out CameraFrame frame)
        {
            var jpeg = _provider.GetCurrentFrameJpeg(jpegQuality);
            if (jpeg == null || jpeg.Length == 0)
            {
                frame = default;
                return false;
            }

            frame = new CameraFrame(jpeg, 0, 0, Time.timeAsDouble, _provider.GetCameraPose(),
                _provider.GetCameraIntrinsics(), poseIsGuaranteed: false);
            return true;
        }
    }
}
