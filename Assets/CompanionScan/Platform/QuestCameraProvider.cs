using System.Collections;
using CompanionScan.Core;
using UnityEngine;

namespace CompanionScan.Platform
{
    public class QuestCameraProvider : MonoBehaviour, ICameraProvider
    {
        private WebCamTexture _webcam;
        private Texture2D _frame;
        private bool _isRequesting;

        public bool IsAvailable => WebCamTexture.devices.Length > 0;

        public void StartCapture()
        {
            if (!IsAvailable)
            {
                return;
            }

#if UNITY_ANDROID
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                if (!_isRequesting)
                {
                    StartCoroutine(RequestAndStart());
                }
                return;
            }
#endif

            StartWebcam();
        }

        public void StopCapture()
        {
            if (_webcam != null)
            {
                _webcam.Stop();
            }
        }

        private void StartWebcam()
        {
            if (_webcam == null)
            {
                var deviceName = ResolveDeviceName();
                _webcam = string.IsNullOrWhiteSpace(deviceName)
                    ? new WebCamTexture()
                    : new WebCamTexture(deviceName);
            }

            if (!_webcam.isPlaying)
            {
                _webcam.Play();
            }
        }

        private string ResolveDeviceName()
        {
            var devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                return null;
            }

            foreach (var device in devices)
            {
                var name = device.name ?? string.Empty;
                var lower = name.ToLowerInvariant();
                if (lower.Contains("passthrough") || lower.Contains("oculus") || lower.Contains("quest"))
                {
                    return name;
                }
            }

            return devices[0].name;
        }

        private IEnumerator RequestAndStart()
        {
            _isRequesting = true;
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            _isRequesting = false;
            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                StartWebcam();
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

        public Matrix4x4 GetCameraIntrinsics()
        {
            return Matrix4x4.identity;
        }

        public Pose GetCameraPose()
        {
            return new Pose(transform.position, transform.rotation);
        }
    }
}

