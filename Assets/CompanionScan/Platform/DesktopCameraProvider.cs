using CompanionScan.Core;
using UnityEngine;

namespace CompanionScan.Platform
{
    public class DesktopCameraProvider : MonoBehaviour, ICameraProvider
    {
        private WebCamTexture _webcam;
        private Texture2D _frame;

        public bool IsAvailable => WebCamTexture.devices.Length > 0;

        public void StartCapture()
        {
            if (!IsAvailable)
            {
                return;
            }

            if (_webcam == null)
            {
                _webcam = new WebCamTexture();
                _webcam.Play();
            }
        }

        public void StopCapture()
        {
            if (_webcam != null)
            {
                _webcam.Stop();
            }
        }

        public byte[] GetCurrentFrameJpeg(int quality = 75)
        {
            if (_webcam == null || !_webcam.isPlaying)
            {
                return null;
            }

            if (_frame == null || _frame.width != _webcam.width || _frame.height != _webcam.height)
            {
                _frame = new Texture2D(_webcam.width, _webcam.height, TextureFormat.RGB24, false);
            }

            _frame.SetPixels32(_webcam.GetPixels32());
            _frame.Apply();
            return _frame.EncodeToJPG(quality);
        }

        /// <summary>A webcam's intrinsics are not known: the zero matrix, which <c>CaptureFrame.HasIntrinsics</c> reads as "none".</summary>
        public Matrix4x4 GetCameraIntrinsics()
        {
            return Matrix4x4.zero;
        }

        public Pose GetCameraPose()
        {
            return new Pose(transform.position, transform.rotation);
        }
    }
}

