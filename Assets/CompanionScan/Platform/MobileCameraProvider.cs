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
    [RequireComponent(typeof(ARCameraManager))]
    public class MobileCameraProvider : MonoBehaviour, ICameraProvider
    {
        private ARCameraManager _cameraManager;
        private Texture2D _frame;

        public bool IsAvailable => _cameraManager != null;

        private void Awake()
        {
            _cameraManager = GetComponent<ARCameraManager>();
        }

        public void StartCapture()
        {
            if (_cameraManager != null)
            {
                _cameraManager.enabled = true;
            }
        }

        public void StopCapture()
        {
            if (_cameraManager != null)
            {
                _cameraManager.enabled = false;
            }
        }

        public byte[] GetCurrentFrameJpeg(int quality = 75)
        {
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
            return Matrix4x4.identity;
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
        public Matrix4x4 GetCameraIntrinsics() => Matrix4x4.identity;
        public Pose GetCameraPose() => new Pose(Vector3.zero, Quaternion.identity);
    }
#endif
}

