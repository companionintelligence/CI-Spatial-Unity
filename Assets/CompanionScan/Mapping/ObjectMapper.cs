using System.Collections.Generic;
using CompanionScan.Core;
using UnityEngine;

#if UNITY_IOS || UNITY_ANDROID
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace CompanionScan.Mapping
{
    public class ObjectMapper : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MappedObjectDatabase database;
        [SerializeField] private float fallbackDistance = 2.0f;

    #if UNITY_IOS || UNITY_ANDROID
        [SerializeField] private ARRaycastManager arRaycastManager;
        private readonly List<ARRaycastHit> _arHits = new List<ARRaycastHit>();
    #endif

        public List<Vector3> MapDetections(DetectionResult result, Texture sourceTexture)
        {
            var positions = new List<Vector3>();
            if (result == null || sourceTexture == null || targetCamera == null)
            {
                return positions;
            }

            for (int i = 0; i < result.bboxes.Count; i++)
            {
                var bbox = result.bboxes[i];
                if (bbox == null || bbox.Length < 4)
                {
                    continue;
                }

                if (!BoundingBoxUtils.TryNormalize(bbox, sourceTexture.width, sourceTexture.height, out Rect normalized))
                {
                    continue;
                }

                float u = normalized.x + normalized.width * 0.5f;
                float v = 1.0f - (normalized.y + normalized.height * 0.5f);

                Vector3 hitPoint = GetWorldPoint(u, v);

                positions.Add(hitPoint);
                if (database != null && i < result.labels.Count)
                {
                    database.Add(new MappedObject
                    {
                        label = result.labels[i],
                        position = hitPoint,
                        score = i < result.scores.Count ? result.scores[i] : 0f
                    });
                }
            }

            return positions;
        }

        private Vector3 GetWorldPoint(float u, float v)
        {
#if UNITY_IOS || UNITY_ANDROID
            if (arRaycastManager != null)
            {
                var screenPoint = new Vector2(u * Screen.width, v * Screen.height);
                if (arRaycastManager.Raycast(screenPoint, _arHits, TrackableType.FeaturePoint | TrackableType.PlaneWithinPolygon))
                {
                    return _arHits[0].pose.position;
                }
            }
#endif
            Ray ray = targetCamera.ViewportPointToRay(new Vector3(u, v, 0));
            Vector3 hitPoint = ray.origin + ray.direction * fallbackDistance;
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                hitPoint = hit.point;
            }
            return hitPoint;
        }
    }
}

