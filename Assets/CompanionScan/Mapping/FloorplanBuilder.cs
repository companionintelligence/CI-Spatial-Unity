using System.Collections.Generic;
using UnityEngine;

namespace CompanionScan.Mapping
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

            // Pins on one or two places, or on one line, enclose no area: that is no polygon.
            if (hull.Count < 3)
            {
                return new List<Vector3>();
            }

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

            var triangles = Triangulate(polygon);
            if (triangles.Length == 0)
            {
                return null; // every point on one line: nothing to show
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

        /// <summary>
        /// Triangles (indices into <paramref name="polygon"/>, three per triangle) that cover a simple polygon
        /// exactly and all face up, whichever way the polygon runs and however concave it is: ear clipping in the
        /// XZ plane. A fan from the first point, which this used to be, turns a triangle of a U-shaped polygon the
        /// other way over the notch, and a polygon that runs counterclockwise from above got downward normals, so
        /// back-face culling hid the floor from above. A point on a straight stretch of the boundary is left out
        /// of the triangles (it stays a vertex, unused). A polygon that crosses itself cannot be covered exactly:
        /// what cannot be clipped is fanned, so a triangle may be turned the wrong way.
        /// </summary>
        public static int[] Triangulate(IReadOnlyList<Vector3> polygon)
        {
            int count = polygon != null ? polygon.Count : 0;
            if (count < 3)
            {
                return new int[0];
            }

            // Clockwise from above is the order that makes a triangle face up (Unity's front faces run clockwise).
            var order = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                order.Add(i);
            }

            if (SignedAreaXZ(polygon) > 0f)
            {
                order.Reverse();
            }

            // Points on a straight stretch (or spikes) are no corners: take them out of the outline first, so
            // a boundary traced with many points along each wall gives no sliver triangles.
            for (int i = 0; i < order.Count && order.Count > 3;)
            {
                int before = order[(i + order.Count - 1) % order.Count];
                int after = order[(i + 1) % order.Count];
                if (Mathf.Abs(TurnUp(polygon[before], polygon[order[i]], polygon[after])) <= CollinearTolerance)
                {
                    order.RemoveAt(i);
                    i = Mathf.Max(0, i - 1);
                }
                else
                {
                    i++;
                }
            }

            var triangles = new List<int>((count - 2) * 3);
            while (order.Count > 3)
            {
                bool clipped = false;
                for (int i = 0; i < order.Count; i++)
                {
                    int previous = order[(i + order.Count - 1) % order.Count];
                    int current = order[i];
                    int next = order[(i + 1) % order.Count];
                    float turn = TurnUp(polygon[previous], polygon[current], polygon[next]);

                    if (Mathf.Abs(turn) <= CollinearTolerance)
                    {
                        // On a straight stretch (or a spike): no triangle, and the point leaves the outline.
                        order.RemoveAt(i);
                        clipped = true;
                        break;
                    }

                    if (turn > 0f && !AnyPointInside(polygon, order, previous, current, next))
                    {
                        triangles.Add(previous);
                        triangles.Add(current);
                        triangles.Add(next);
                        order.RemoveAt(i);
                        clipped = true;
                        break;
                    }
                }

                if (!clipped)
                {
                    // No ear: a polygon that crosses itself. Fan what is left rather than lose it.
                    for (int i = 1; i < order.Count - 1; i++)
                    {
                        triangles.Add(order[0]);
                        triangles.Add(order[i]);
                        triangles.Add(order[i + 1]);
                    }

                    return triangles.ToArray();
                }
            }

            if (order.Count == 3 && TurnUp(polygon[order[0]], polygon[order[1]], polygon[order[2]]) > CollinearTolerance)
            {
                triangles.Add(order[0]);
                triangles.Add(order[1]);
                triangles.Add(order[2]);
            }

            return triangles.ToArray();
        }

        /// <summary>Below this a turn is no turn (twice the area of the triangle, in square metres).</summary>
        private const float CollinearTolerance = 1e-6f;

        /// <summary>
        /// Positive when going from <paramref name="a"/> to <paramref name="b"/> to <paramref name="c"/> runs
        /// clockwise seen from above (the way that faces up): twice the area of the triangle, signed.
        /// </summary>
        private static float TurnUp(Vector3 a, Vector3 b, Vector3 c)
        {
            return (b.z - a.z) * (c.x - b.x) - (b.x - a.x) * (c.z - b.z);
        }

        /// <summary>Twice the signed area of the polygon in the XZ plane; positive counterclockwise seen from above.</summary>
        private static float SignedAreaXZ(IReadOnlyList<Vector3> polygon)
        {
            float twice = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                twice += a.x * b.z - b.x * a.z;
            }

            return twice;
        }

        private static bool AnyPointInside(IReadOnlyList<Vector3> polygon, List<int> order, int a, int b, int c)
        {
            for (int i = 0; i < order.Count; i++)
            {
                int index = order[i];
                if (index == a || index == b || index == c)
                {
                    continue;
                }

                var p = polygon[index];

                // A point at the same place as a corner is not inside; a point on an edge or in the triangle is.
                if (SamePlace(p, polygon[a]) || SamePlace(p, polygon[b]) || SamePlace(p, polygon[c]))
                {
                    continue;
                }

                // Inside a clockwise triangle: on the clockwise side of (or on) all three edges.
                if (TurnUp(polygon[a], polygon[b], p) >= -CollinearTolerance
                    && TurnUp(polygon[b], polygon[c], p) >= -CollinearTolerance
                    && TurnUp(polygon[c], polygon[a], p) >= -CollinearTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SamePlace(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x) <= 1e-6f && Mathf.Abs(a.z - b.z) <= 1e-6f;
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

