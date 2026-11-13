using System.Collections.Generic;
using CompanionScan.Core;
using UnityEngine;

namespace CompanionScan.Platform
{
    /// <summary>
    /// Passthrough camera frames on Meta Quest 3 / 3S through <see cref="WebCamTexture"/>.
    ///
    /// This is the fallback path: SpatialCapturePipeline uses it only when preferArCameraOnQuest is
    /// off. The shipped scene keeps that option on, so Quest builds capture through
    /// <see cref="MobileCameraProvider"/> (AR Foundation and Meta OpenXR's camera subsystem) instead.
    ///
    /// Horizon OS gates the passthrough cameras behind <see cref="HeadsetCameraPermission"/>
    /// (Passthrough Camera API, Horizon OS v74+), and WebCamTexture checks
    /// <see cref="AndroidCameraPermission"/> before it opens a camera, so in Android players the
    /// webcam starts only once both are granted (<see cref="PassthroughCameraPermission"/>, through
    /// <see cref="CameraPermissionGate"/>): without the headset camera permission the passthrough
    /// cameras may not be listed at all.
    /// </summary>
    public class QuestCameraProvider : MonoBehaviour, ICameraProvider
    {
        public const string HeadsetCameraPermission = PassthroughCameraPermission.HeadsetCamera;
        public const string AndroidCameraPermission = PassthroughCameraPermission.AndroidCamera;

        /// <summary>Runtime permissions the camera needs, in the order they are requested.</summary>
        public static IReadOnlyList<string> RequiredPermissions => PassthroughCameraPermission.RequiredPermissions;

        private WebCamTexture _webcam;
        private Texture2D _frame;
        private CameraPermissionGate _permissionGate;
        private bool _loggedDevices;
        private bool _loggedNoDevice;
        private bool _loggedNotReady;
        private bool _loggedEmptyFrame;

        [Header("Capture Settings")]
        [SerializeField] private int requestedWidth = 1280;
        [SerializeField] private int requestedHeight = 960;
        [SerializeField] private int requestedFps = 30;

        public bool IsAvailable => WebCamTexture.devices.Length > 0;

        private void Awake()
        {
            _permissionGate = CreatePermissionGate();
        }

        /// <summary>Android players of this project are Meta Quest builds; elsewhere the permissions do not apply.</summary>
        private CameraPermissionGate CreatePermissionGate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new CameraPermissionGate(
                () => PassthroughCameraPermission.IsGranted,
                PassthroughCameraPermission.RequestInBackground,
                () => _webcam != null && _webcam.isPlaying,
                on =>
                {
                    if (on)
                    {
                        StartWebcam();
                    }
                    else
                    {
                        StopWebcam();
                    }
                });
#else
            return null;
#endif
        }

        public void StartCapture()
        {
            if (_permissionGate != null)
            {
                // Asks first when needed; the gate starts the webcam, on the main thread, once granted.
                _permissionGate.StartCapture(Time.unscaledTime);
                return;
            }

            StartWebcam();
        }

        public void StopCapture()
        {
            if (_permissionGate != null)
            {
                _permissionGate.StopCapture();
                return;
            }

            StopWebcam();
        }

        private void Update()
        {
            // Starts the webcam once the permissions are granted (and retries one that failed to start).
            _permissionGate?.Update(Time.unscaledTime);
        }

        private void StopWebcam()
        {
            if (_webcam != null)
            {
                _webcam.Stop();
            }
        }

        private void StartWebcam()
        {
            if (!IsAvailable)
            {
                if (!_loggedNoDevice)
                {
                    _loggedNoDevice = true;
                    Debug.LogWarning("[QuestCamera] No camera available.");
                }

                return;
            }

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

        public byte[] GetCurrentFrameJpeg(int quality = 75)
        {
            if (_webcam == null || !_webcam.isPlaying)
            {
                // Ask again if capture is still waiting for the permissions (e.g. after a dismissed dialog).
                _permissionGate?.OnFrameRequested();
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

