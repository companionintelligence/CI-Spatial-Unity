using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        [SerializeField] private MonoBehaviour environmentRaycastManager;
        [SerializeField] private bool preferEnvironmentRaycast = true;
        [SerializeField] private bool requireEnvironmentRaycast = false;

        private bool _loggedEnvironmentUnsupported;
        private bool _loggedEnvironmentPermission;
        private bool _loggedEnvironmentMiss;

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
            if (preferEnvironmentRaycast)
            {
                bool envAvailable = IsEnvironmentRaycastAvailable();
                if (envAvailable && TryEnvironmentRaycast(ray, out point, out normal))
                {
                    hitType = RaycastHitType.Environment;
                    return true;
                }

                if (requireEnvironmentRaycast && envAvailable)
                {
                    if (!_loggedEnvironmentMiss)
                    {
                        _loggedEnvironmentMiss = true;
                        Debug.LogWarning("[Raycaster] Environment raycast required but no hit returned.");
                    }
                    return false;
                }
            }
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
            if (preferEnvironmentRaycast)
            {
                bool envAvailable = IsEnvironmentRaycastAvailable();
                if (envAvailable && TryEnvironmentRaycast(ray, out point, out normal))
                {
                    hitType = RaycastHitType.Environment;
                    return true;
                }

                if (requireEnvironmentRaycast && envAvailable)
                {
                    if (!_loggedEnvironmentMiss)
                    {
                        _loggedEnvironmentMiss = true;
                        Debug.LogWarning("[Raycaster] Environment raycast required but no hit returned.");
                    }
                    return false;
                }
            }
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

        private bool TryEnvironmentRaycast(Ray ray, out Vector3 point, out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.up;

            var manager = ResolveEnvironmentRaycastManager();
            if (manager == null)
            {
                return false;
            }

            var managerType = manager.GetType();
            var method = managerType.GetMethods()
                .FirstOrDefault(m => m.Name == "Raycast"
                                     && m.GetParameters().Length == 2
                                     && m.GetParameters()[0].ParameterType == typeof(Ray)
                                     && m.GetParameters()[1].ParameterType.IsByRef);
            if (method == null)
            {
                return false;
            }

            var hitType = method.GetParameters()[1].ParameterType.GetElementType();
            var hitInfo = hitType != null ? Activator.CreateInstance(hitType) : null;
            var args = new object[] { ray, hitInfo };
            var result = method.Invoke(manager, args);
            if (result is bool hit && hit)
            {
                hitInfo = args[1];
                if (!TryReadVector3(hitInfo, "point", out point))
                {
                    return false;
                }

                if (!TryReadVector3(hitInfo, "normal", out normal))
                {
                    normal = Vector3.up;
                }

                return true;
            }

            return false;
        }

        private bool IsEnvironmentRaycastAvailable()
        {
            var manager = ResolveEnvironmentRaycastManager();
            if (manager == null)
            {
                return false;
            }

            var managerType = manager.GetType();
            var supportedProp = managerType.GetProperty("IsSupported", BindingFlags.Public | BindingFlags.Static);
            if (supportedProp != null && supportedProp.PropertyType == typeof(bool))
            {
                bool supported = (bool)supportedProp.GetValue(null);
                if (!supported)
                {
                    if (!_loggedEnvironmentUnsupported)
                    {
                        _loggedEnvironmentUnsupported = true;
                        Debug.LogWarning("[Raycaster] Environment raycast not supported on this device.");
                    }
                    return false;
                }
            }

#if UNITY_ANDROID
            const string permission = "com.oculus.permission.USE_SCENE";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
            {
                if (!_loggedEnvironmentPermission)
                {
                    _loggedEnvironmentPermission = true;
                    Debug.LogWarning("[Raycaster] Environment raycast permission not granted (USE_SCENE).");
                }
                return false;
            }
#endif

            return true;
        }

        private MonoBehaviour ResolveEnvironmentRaycastManager()
        {
            if (environmentRaycastManager != null)
            {
                return environmentRaycastManager;
            }

            var type = FindType("Meta.XR.EnvironmentRaycastManager")
                       ?? FindType("EnvironmentRaycastManager");
            if (type == null)
            {
                return null;
            }

            var found = UnityEngine.Object.FindObjectOfType(type) as MonoBehaviour;
            environmentRaycastManager = found;
            return environmentRaycastManager;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool TryReadVector3(object obj, string memberName, out Vector3 value)
        {
            value = Vector3.zero;
            if (obj == null)
            {
                return false;
            }

            var type = obj.GetType();
            var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(Vector3))
            {
                value = (Vector3)prop.GetValue(obj);
                return true;
            }

            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(Vector3))
            {
                value = (Vector3)field.GetValue(obj);
                return true;
            }

            return false;
        }
    }
}

