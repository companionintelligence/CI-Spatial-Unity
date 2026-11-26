using CompanionScan.Core;
using UnityEngine;

namespace CompanionScan.Replay
{
    /// <summary>
    /// The camera backend of a recorded session: each capture is the next recorded frame, with the pose
    /// and the intrinsics it was recorded with (not the pose of whatever camera the scene has now).
    /// </summary>
    public class ReplayCameraSource : MonoBehaviour, ICameraSource
    {
        private ScanRecording _recording;
        private int _next;

        /// <summary>The session being played.</summary>
        public ScanRecording Recording
        {
            get => _recording;
            set
            {
                _recording = value;
                _next = 0;
            }
        }

        /// <summary>When true, capture starts over after the last frame; otherwise it gives no frame.</summary>
        public bool Loop { get; set; }

        /// <summary>Index of the frame the next capture returns.</summary>
        public int NextFrameIndex => _next;

        /// <summary>How many frames the recording has left (all of them when it loops).</summary>
        public int FramesLeft => _recording == null ? 0 : (Loop ? _recording.frames.Count : _recording.frames.Count - _next);

        public bool IsAvailable => _recording != null && _recording.frames.Count > 0;

        public void StartCapture()
        {
        }

        public void StopCapture()
        {
        }

        public bool TryCaptureFrame(int jpegQuality, out CameraFrame frame)
        {
            var recorded = Peek();
            if (recorded == null || recorded.jpeg == null || recorded.jpeg.Length == 0)
            {
                frame = default;
                return false;
            }

            Advance();
            frame = new CameraFrame(recorded.jpeg, recorded.width, recorded.height, recorded.timestamp, recorded.pose,
                recorded.intrinsics);
            return true;
        }

        // The ICameraProvider members describe the frame the next capture returns. They do not advance:
        // only TryCaptureFrame does, so a caller that reads the three one after the other (a plain
        // provider's way) gets one consistent frame, and then calls Advance itself.
        public byte[] GetCurrentFrameJpeg(int quality = 75) => Peek()?.jpeg;

        public Matrix4x4 GetCameraIntrinsics() => Peek()?.intrinsics ?? Matrix4x4.zero;

        public Pose GetCameraPose() => Peek()?.pose ?? Pose.identity;

        /// <summary>Moves to the following frame (wrapping when <see cref="Loop"/> is set).</summary>
        public void Advance()
        {
            if (_recording == null || _recording.frames.Count == 0)
            {
                return;
            }

            _next++;
            if (Loop && _next >= _recording.frames.Count)
            {
                _next = 0;
            }
        }

        private ReplayFrame Peek()
        {
            if (_recording == null || _next < 0 || _next >= _recording.frames.Count)
            {
                return null;
            }

            return _recording.frames[_next];
        }
    }
}
