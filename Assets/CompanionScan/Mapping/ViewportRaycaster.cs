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
            normal = Vector3.up;
            point = Vector3.zero;

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
                    return true;
                }
            }
    #endif

            Ray ray = targetCamera.ViewportPointToRay(new Vector3(uv.x, uv.y, 0));
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastMask))
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }

            point = ray.origin + ray.direction * fallbackDistance;
            return false;
        }
    }
}

