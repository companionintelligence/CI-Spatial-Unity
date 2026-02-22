using System.Collections.Generic;
using UnityEngine;

namespace HouseMapper.Mapping
{
    public static class FloorplanBuilder
    {
        public static List<Vector3> BuildFloorPolygon(IReadOnlyList<Vector3> boundaryPoints, IReadOnlyList<SpatialPin> pins, float floorY)
        {
            if (boundaryPoints != null && boundaryPoints.Count >= 3)
            {
                return new List<Vector3>(boundaryPoints);
            }

            var candidate = new List<Vector2>();
            if (pins != null)
            {
                for (int i = 0; i < pins.Count; i++)
                {
                    candidate.Add(new Vector2(pins[i].position.x, pins[i].position.z));
                }
            }

            if (candidate.Count < 3)
            {
                return new List<Vector3>();
            }

            var hull = BuildConvexHull(candidate);
            var polygon = new List<Vector3>(hull.Count);
            for (int i = 0; i < hull.Count; i++)
            {
                polygon.Add(new Vector3(hull[i].x, floorY, hull[i].y));
            }

            return polygon;
        }

        public static Mesh BuildFloorMesh(IReadOnlyList<Vector3> polygon, Transform root, float floorY)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return null;
            }

            var vertices = new Vector3[polygon.Count];
            for (int i = 0; i < polygon.Count; i++)
            {
                var point = new Vector3(polygon[i].x, floorY, polygon[i].z);
                vertices[i] = root != null ? root.InverseTransformPoint(point) : point;
            }

            int triangleCount = (polygon.Count - 2) * 3;
            var triangles = new int[triangleCount];
            int index = 0;
            for (int i = 1; i < polygon.Count - 1; i++)
            {
                triangles[index++] = 0;
                triangles[index++] = i;
                triangles[index++] = i + 1;
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Bounds ComputeBounds(IReadOnlyList<Vector3> boundaryPoints, IReadOnlyList<SpatialPin> pins)
        {
            bool hasPoint = false;
            Bounds bounds = new Bounds();

            if (boundaryPoints != null)
            {
                for (int i = 0; i < boundaryPoints.Count; i++)
                {
                    if (!hasPoint)
                    {
                        bounds = new Bounds(boundaryPoints[i], Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(boundaryPoints[i]);
                    }
                }
            }

            if (pins != null)
            {
                for (int i = 0; i < pins.Count; i++)
                {
                    if (!hasPoint)
                    {
                        bounds = new Bounds(pins[i].position, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(pins[i].position);
                    }
                }
            }

            return bounds;
        }

        private static List<Vector2> BuildConvexHull(List<Vector2> points)
        {
            points.Sort((a, b) =>
            {
                int cmp = a.x.CompareTo(b.x);
                return cmp != 0 ? cmp : a.y.CompareTo(b.y);
            });

            var lower = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], points[i]) <= 0f)
                {
                    lower.RemoveAt(lower.Count - 1);
                }
                lower.Add(points[i]);
            }

            var upper = new List<Vector2>();
            for (int i = points.Count - 1; i >= 0; i--)
            {
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], points[i]) <= 0f)
                {
                    upper.RemoveAt(upper.Count - 1);
                }
                upper.Add(points[i]);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }
    }
}
