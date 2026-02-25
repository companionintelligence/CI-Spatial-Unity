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
        private bool _loggedDevices;
        private bool _loggedNotReady;
        private bool _loggedEmptyFrame;

        [Header("Capture Settings")]
        [SerializeField] private int requestedWidth = 1280;
        [SerializeField] private int requestedHeight = 960;
        [SerializeField] private int requestedFps = 30;

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
                LogDevicesIfNeeded();
                var deviceName = ResolveDeviceName();
                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    _webcam = CreateWebcam(null);
                }
                else
                {
                    _webcam = CreateWebcam(deviceName);
                }
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
                if (lower.Contains("passthrough") || lower.Contains("oculus") || lower.Contains("quest") || lower.Contains("rgb") || lower.Contains("color"))
                {
                    return name;
                }
            }

            return devices[0].name;
        }

        private WebCamTexture CreateWebcam(string deviceName)
        {
            int width = requestedWidth > 0 ? requestedWidth : 0;
            int height = requestedHeight > 0 ? requestedHeight : 0;
            int fps = requestedFps > 0 ? requestedFps : 0;

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return width > 0 && height > 0
                    ? new WebCamTexture(width, height, fps)
                    : new WebCamTexture();
            }

            return width > 0 && height > 0
                ? new WebCamTexture(deviceName, width, height, fps)
                : new WebCamTexture(deviceName);
        }

        private void LogDevicesIfNeeded()
        {
            if (_loggedDevices)
            {
                return;
            }
            _loggedDevices = true;
            var devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.LogWarning("[QuestCamera] No WebCamTexture devices found.");
                return;
            }

            for (int i = 0; i < devices.Length; i++)
            {
                Debug.Log($"[QuestCamera] Device[{i}] = '{devices[i].name}'");
            }
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

            if (_webcam.width <= 16 || _webcam.height <= 16)
            {
                if (!_loggedNotReady)
                {
                    _loggedNotReady = true;
                    Debug.LogWarning($"[QuestCamera] Webcam not ready (size={_webcam.width}x{_webcam.height}).");
                }
                return null;
            }

            if (_frame == null || _frame.width != _webcam.width || _frame.height != _webcam.height)
            {
                _frame = new Texture2D(_webcam.width, _webcam.height, TextureFormat.RGB24, false);
            }

            var pixels = _webcam.GetPixels32();
            if (pixels == null || pixels.Length == 0)
            {
                if (!_loggedEmptyFrame)
                {
                    _loggedEmptyFrame = true;
                    Debug.LogWarning("[QuestCamera] Webcam returned empty pixel buffer.");
                }
                return null;
            }

            _frame.SetPixels32(pixels);
            _frame.Apply();
            return _frame.EncodeToJPG(quality);
        }

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

