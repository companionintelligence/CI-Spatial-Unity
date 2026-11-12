using System.Collections.Generic;
using CompanionScan.Core;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace CompanionScan.Platform
{
    /// <summary>
    /// Passthrough camera frames on Meta Quest 3 / 3S through <see cref="WebCamTexture"/>.
    ///
    /// Horizon OS gates the passthrough cameras behind <see cref="HeadsetCameraPermission"/>
    /// (Passthrough Camera API, Horizon OS v74+), so the provider asks for it before it looks for
    /// cameras or opens one. <see cref="AndroidCameraPermission"/> is asked for in the same dialog
    /// because Unity's WebCamTexture checks it before opening a camera; drop it from
    /// <see cref="RequiredPermissions"/> and from AndroidManifest.xml only once capture is verified
    /// on a headset without it.
    /// </summary>
    public class QuestCameraProvider : MonoBehaviour, ICameraProvider
    {
        public const string HeadsetCameraPermission = "horizonos.permission.HEADSET_CAMERA";
        public const string AndroidCameraPermission = "android.permission.CAMERA";

        /// <summary>Runtime permissions the camera needs, in the order they are requested.</summary>
        public static readonly IReadOnlyList<string> RequiredPermissions = new[]
        {
            HeadsetCameraPermission,
            AndroidCameraPermission
        };

        private WebCamTexture _webcam;
        private Texture2D _frame;
#if UNITY_ANDROID && !UNITY_EDITOR
        private bool _captureRequested;
        private bool _isRequesting;
        private bool _permissionDenied;
#endif
        private bool _loggedDevices;
        private bool _loggedNoDevice;
        private bool _loggedNotReady;
        private bool _loggedEmptyFrame;

        [Header("Capture Settings")]
        [SerializeField] private int requestedWidth = 1280;
        [SerializeField] private int requestedHeight = 960;
        [SerializeField] private int requestedFps = 30;

        public bool IsAvailable => WebCamTexture.devices.Length > 0;

        public void StartCapture()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _captureRequested = true;

            // Ask first: without the headset camera permission Horizon OS may not list the
            // passthrough cameras at all. Update() starts the camera once both are granted.
            if (!HasCameraPermissions())
            {
                RequestCameraPermissions();
                return;
            }
#endif

            StartWebcam();
        }

        public void StopCapture()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _captureRequested = false;
#endif
            if (_webcam != null)
            {
                _webcam.Stop();
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void Update()
        {
            // Permission callbacks only record the answer; the camera is started here, on the main thread.
            if (_captureRequested && !_isRequesting && !_permissionDenied &&
                (_webcam == null || !_webcam.isPlaying) && HasCameraPermissions())
            {
                StartWebcam();
            }
        }

        private static bool HasCameraPermissions()
        {
            foreach (var permission in RequiredPermissions)
            {
                if (!Permission.HasUserAuthorizedPermission(permission))
                {
                    return false;
                }
            }

            return true;
        }

        private void RequestCameraPermissions()
        {
            if (_isRequesting || _permissionDenied)
            {
                return;
            }

            var missing = new List<string>();
            foreach (var permission in RequiredPermissions)
            {
                if (!Permission.HasUserAuthorizedPermission(permission))
                {
                    missing.Add(permission);
                }
            }

            var pending = new HashSet<string>(missing);
            var denied = new List<string>();

            void Answer(string permission, bool granted, bool dismissed)
            {
                if (!pending.Remove(permission))
                {
                    return;
                }

                if (!granted && !dismissed)
                {
                    denied.Add(permission);
                }

                if (pending.Count > 0)
                {
                    return;
                }

                _isRequesting = false;
                if (denied.Count > 0)
                {
                    // Do not ask again this session; StopCapture/StartCapture will not re-prompt either.
                    _permissionDenied = true;
                    Debug.LogWarning("[QuestCamera] Camera permission denied (" + string.Join(", ", denied) +
                                     "). Allow camera access for this app in the headset settings to capture frames.");
                }
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += permission => Answer(permission, true, false);
            // Also raised for "don't ask again".
            callbacks.PermissionDenied += permission => Answer(permission, false, false);
            // Dialog dismissed without an answer: ask again the next time capture starts.
            callbacks.PermissionRequestDismissed += permission => Answer(permission, false, true);

            _isRequesting = true;
            Debug.Log("[QuestCamera] Requesting " + string.Join(", ", missing));
            Permission.RequestUserPermissions(missing.ToArray(), callbacks);
        }
#endif

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

