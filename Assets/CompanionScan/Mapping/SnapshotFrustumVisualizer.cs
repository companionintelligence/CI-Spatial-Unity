using System.Collections.Generic;
using UnityEngine;

namespace CompanionScan.Mapping
{
    /// <summary>
    /// Creates a simple line-rendered frustum frame and freezes it in place
    /// at the moment of capture.
    /// </summary>
    public class SnapshotFrustumVisualizer : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float lineWidth = 0.0025f;
        [SerializeField] private Color lineColor = new Color(0.95f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private string frameRootName = "FrustumFrame";

        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;
        }

        public void FreezeFrustum(float distance)
        {
            if (distance <= 0f)
            {
                return;
            }

            var cam = ResolveCamera();
            if (cam == null)
            {
                Debug.LogWarning("[Frustum] No camera found for snapshot frame.");
                return;
            }

            var root = new GameObject(frameRootName + "_" + Time.frameCount);
            root.transform.SetParent(transform, false);

            var corners = GetFarPlaneCorners(cam, distance);
            var camPos = cam.transform.position;

            // Rectangle at far plane
            var rect = BuildLineRenderer(root.transform, "FarRect", loop: true);
            rect.positionCount = 4;
            rect.SetPositions(new[] { corners[0], corners[1], corners[2], corners[3] });

            // Rays from camera to corners
            for (int i = 0; i < corners.Count; i++)
            {
                var ray = BuildLineRenderer(root.transform, "Ray_" + i, loop: false);
                ray.positionCount = 2;
                ray.SetPosition(0, camPos);
                ray.SetPosition(1, corners[i]);
            }
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            targetCamera = Camera.main;
            return targetCamera;
        }

        private List<Vector3> GetFarPlaneCorners(Camera cam, float distance)
        {
            var corners = new List<Vector3>(4)
            {
                cam.ViewportToWorldPoint(new Vector3(0f, 0f, distance)),
                cam.ViewportToWorldPoint(new Vector3(0f, 1f, distance)),
                cam.ViewportToWorldPoint(new Vector3(1f, 1f, distance)),
                cam.ViewportToWorldPoint(new Vector3(1f, 0f, distance))
            };

            return corners;
        }

        private LineRenderer BuildLineRenderer(Transform parent, string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            lr.material = new Material(shader);
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;

            return lr;
        }
    }
}
