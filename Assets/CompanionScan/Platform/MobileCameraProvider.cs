using CompanionScan.Core;
using UnityEngine;

#if UNITY_IOS || UNITY_ANDROID
using Unity.Collections;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace CompanionScan.Platform
{
#if UNITY_IOS || UNITY_ANDROID
    /// <summary>
    /// Camera frames through AR Foundation's <see cref="ARCameraManager"/>. On Meta Quest 3 / 3S this
    /// is the passthrough capture path of the shipped scene: SpatialCapturePipeline creates this
    /// provider when preferArCameraOnQuest is on, and Meta OpenXR's camera subsystem serves it.
    ///
    /// In Android players the ARCameraManager runs only once the passthrough camera permissions
    /// (<see cref="PassthroughCameraPermission"/>) are granted. Meta OpenXR checks HEADSET_CAMERA once,
    /// when its camera subsystem starts, and does not start it again after a later grant, so
    /// <see cref="CameraPermissionGate"/> holds the camera back, asks when capture starts and
    /// switches it on once the permissions are granted.
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class MobileCameraProvider : MonoBehaviour, ICameraProvider
    {
        private ARCameraManager _cameraManager;
        private Texture2D _frame;
        private CameraPermissionGate _permissionGate;
        private bool _loggedWaiting;

        public bool IsAvailable => _cameraManager != null;

        /// <summary>True while capture is requested but the camera waits for its permissions.</summary>
        public bool IsWaitingForPermission => _permissionGate != null && _permissionGate.IsWaitingForPermission;

        private void Awake()
        {
            _cameraManager = GetComponent<ARCameraManager>();
            _permissionGate = CreatePermissionGate(_cameraManager);
            _permissionGate?.Initialize();
        }

        /// <summary>
        /// Android players of this project are Meta Quest builds (Meta OpenXR; Quest 3 / 3S only), so
        /// they get the gate. Elsewhere the passthrough camera permissions do not apply.
        /// </summary>
        private static CameraPermissionGate CreatePermissionGate(ARCameraManager cameraManager)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (cameraManager != null)
            {
                return new CameraPermissionGate(
                    () => PassthroughCameraPermission.IsGranted,
                    PassthroughCameraPermission.RequestInBackground,
                    () => cameraManager.enabled,
                    on => cameraManager.enabled = on);
            }
#endif
            return null;
        }

        private void Update()
        {
            _permissionGate?.Update(Time.unscaledTime);
        }

        public void StartCapture()
        {
            if (_cameraManager == null)
            {
                return;
            }

            if (_permissionGate != null)
            {
                _loggedWaiting = false;
                _permissionGate.StartCapture(Time.unscaledTime);
                return;
            }

            _cameraManager.enabled = true;
        }

        public void StopCapture()
        {
            if (_cameraManager == null)
            {
                return;
            }

            if (_permissionGate != null)
            {
                _permissionGate.StopCapture();
                return;
            }

            _cameraManager.enabled = false;
        }

        public byte[] GetCurrentFrameJpeg(int quality = 75)
        {
            if (IsWaitingForPermission)
            {
                _permissionGate.OnFrameRequested();
                if (!_loggedWaiting)
                {
                    _loggedWaiting = true;
                    Debug.LogWarning(PassthroughCameraPermission.LastKnown == PassthroughCameraPermission.State.Denied
                        ? "[MobileCamera] " + PassthroughCameraPermission.DeniedMessage
                        : "[MobileCamera] No frame yet: the camera starts once the headset camera permissions are granted.");
                }

                return null;
            }

            if (_cameraManager == null || !_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            {
                return null;
            }

            using (image)
            {
                TextureFormat textureFormat = TextureFormat.RGB24;
                if (_frame == null || _frame.width != image.width || _frame.height != image.height)
                {
                    _frame = new Texture2D(image.width, image.height, textureFormat, false);
                }

                var conversionParams = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0, 0, image.width, image.height),
                    outputDimensions = new Vector2Int(image.width, image.height),
                    outputFormat = TextureFormat.RGB24,
                    transformation = XRCpuImage.Transformation.MirrorY
                };

                var bufferSize = image.GetConvertedDataSize(conversionParams);
                var buffer = new NativeArray<byte>(bufferSize, Allocator.Temp);
                image.Convert(conversionParams, buffer);
                _frame.LoadRawTextureData(buffer);
                _frame.Apply();
                buffer.Dispose();
                return _frame.EncodeToJPG(quality);
            }
        }

        public Matrix4x4 GetCameraIntrinsics()
        {
            if (_cameraManager != null && _cameraManager.TryGetIntrinsics(out var intrinsics))
            {
                var matrix = Matrix4x4.zero;
                matrix.m00 = intrinsics.focalLength.x;
                matrix.m11 = intrinsics.focalLength.y;
                matrix.m02 = intrinsics.principalPoint.x;
                matrix.m12 = intrinsics.principalPoint.y;
                matrix.m22 = 1f;
                return matrix;
            }

            return Matrix4x4.zero;
        }

        public Pose GetCameraPose()
        {
            return new Pose(transform.position, transform.rotation);
        }
    }
#else
    public class MobileCameraProvider : MonoBehaviour, ICameraProvider
    {
        public bool IsAvailable => false;
        public void StartCapture() { }
        public void StopCapture() { }
        public byte[] GetCurrentFrameJpeg(int quality = 75) => null;
        public Matrix4x4 GetCameraIntrinsics() => Matrix4x4.zero;
        public Pose GetCameraPose() => new Pose(Vector3.zero, Quaternion.identity);
    }
#endif
}

