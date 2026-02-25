using System.Collections.Generic;
using UnityEngine;

#if UNITY_IOS || UNITY_ANDROID
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace CompanionScan.Mapping
{
    public class ViewportRaycaster : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float fallbackDistance = 2.0f;
        [SerializeField] private float maxDistance = 10.0f;
        [SerializeField] private LayerMask raycastMask = ~0;

        /// <summary>Runtime-adjustable fallback distance (metres).
        /// Used when no AR/physics hit is found.</summary>
        public float FallbackDistance
        {
            get => fallbackDistance;
            set => fallbackDistance = Mathf.Max(0f, value);
        }

    #if UNITY_IOS || UNITY_ANDROID
        [SerializeField] private ARRaycastManager arRaycastManager;
        private readonly List<ARRaycastHit> _arHits = new List<ARRaycastHit>();
    #endif

        public bool TryRaycastViewport(Vector2 uv, out Vector3 point, out Vector3 normal)
        {
            return TryRaycastViewport(uv, out point, out normal, out _);
        }

        public bool TryRaycastViewport(Vector2 uv, out Vector3 point, out Vector3 normal, out RaycastHitType hitType)
        {
            normal = Vector3.up;
            point = Vector3.zero;
            hitType = RaycastHitType.Fallback;

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                return false;
            }

    #if UNITY_IOS || UNITY_ANDROID
            if (arRaycastManager != null)
            {
                var screenPoint = new Vector2(uv.x * Screen.width, uv.y * Screen.height);
                if (arRaycastManager.Raycast(screenPoint, _arHits, TrackableType.FeaturePoint | TrackableType.PlaneWithinPolygon))
                {
                    point = _arHits[0].pose.position;
                    normal = _arHits[0].pose.up;
                    hitType = RaycastHitType.ArPlane;
                    return true;
                }
            }
    #endif

            Ray ray = targetCamera.ViewportPointToRay(new Vector3(uv.x, uv.y, 0));
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastMask))
            {
                point = hit.point;
                normal = hit.normal;
                hitType = RaycastHitType.Physics;
                return true;
            }

            point = ray.origin + ray.direction * fallbackDistance;
            return false;
        }

        public bool TryRaycastCapture(CaptureFrame frame, Vector2 pixel, out Vector3 point, out Vector3 normal, out RaycastHitType hitType)
        {
            return TryRaycastCapture(frame, pixel, true, out point, out normal, out hitType);
        }

        public bool TryRaycastCapture(CaptureFrame frame, Vector2 pixel, bool allowFallback,
                                      out Vector3 point, out Vector3 normal, out RaycastHitType hitType)
        {
            normal = Vector3.up;
            point = Vector3.zero;
            hitType = RaycastHitType.Fallback;

            if (frame == null)
            {
                return TryRaycastViewport(Vector2.zero, out point, out normal, out hitType);
            }

    #if UNITY_IOS || UNITY_ANDROID
            if (arRaycastManager != null && frame.imageWidth > 0 && frame.imageHeight > 0)
            {
                var screenPoint = new Vector2(pixel.x / frame.imageWidth * Screen.width,
                                             (1f - (pixel.y / frame.imageHeight)) * Screen.height);
                if (arRaycastManager.Raycast(screenPoint, _arHits, TrackableType.FeaturePoint | TrackableType.PlaneWithinPolygon))
                {
                    point = _arHits[0].pose.position;
                    normal = _arHits[0].pose.up;
                    hitType = RaycastHitType.ArPlane;
                    return true;
                }
            }
    #endif

            var ray = BuildRayFromIntrinsics(frame, pixel);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastMask))
            {
                point = hit.point;
                normal = hit.normal;
                hitType = RaycastHitType.Physics;
                return true;
            }

            if (allowFallback)
            {
                point = ray.origin + ray.direction * fallbackDistance;
            }
            return false;
        }

        public static Ray BuildRayFromIntrinsics(CaptureFrame frame, Vector2 pixel)
        {
            if (frame == null || !frame.HasIntrinsics)
            {
                var origin = frame != null ? frame.cameraPose.position : Vector3.zero;
                var direction = frame != null ? (frame.cameraPose.rotation * Vector3.forward) : Vector3.forward;
                return new Ray(origin, direction.normalized);
            }

            float fx = frame.cameraIntrinsics.m00;
            float fy = frame.cameraIntrinsics.m11;
            float cx = frame.cameraIntrinsics.m02;
            float cy = frame.cameraIntrinsics.m12;

            // Pixel coordinates are top-left origin; convert to camera space with Y up.
            float x = (pixel.x - cx) / fx;
            float y = (pixel.y - cy) / fy;
            var dirCamera = new Vector3(x, -y, 1f).normalized;
            var dirWorld = frame.cameraPose.rotation * dirCamera;
            return new Ray(frame.cameraPose.position, dirWorld.normalized);
        }
    }
}

