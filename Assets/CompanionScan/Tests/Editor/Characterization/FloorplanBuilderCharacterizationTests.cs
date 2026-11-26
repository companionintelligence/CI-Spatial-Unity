using System.Collections.Generic;
using System.Linq;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// Characterization tests: they record what <see cref="FloorplanBuilder"/> does today. A test
    /// with "Currently" in its name records a result that looks doubtful, not a requirement.
    /// The builder works in the units and the world space of its input and never scales or
    /// converts it (the exporter labels the result "meters"). It computes no area or perimeter.
    /// Winding below is read from above: x to the right, z up the page.
    /// </summary>
    public class FloorplanBuilderCharacterizationTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        // ---- BuildFloorPolygon: from the boundary ----

        [Test]
        public void BuildFloorPolygon_WithThreeOrMoreBoundaryPoints_ReturnsThemUnchanged_IgnoringThePinsAndFloorY()
        {
            var boundary = new List<Vector3> { V(0f, 0.3f, 0f), V(4f, 0.3f, 0f), V(4f, 0.3f, 3f), V(0f, 0.3f, 3f) };
            var pins = new[] { PinAt(10f, 0f, 10f), PinAt(11f, 0f, 10f), PinAt(10f, 0f, 11f) };

            var polygon = FloorplanBuilder.BuildFloorPolygon(boundary, pins, 9f);

            CollectionAssert.AreEqual(boundary, polygon);
            polygon.Clear();
            Assert.AreEqual(4, boundary.Count, "the polygon is a copy of the boundary, not the boundary itself");
        }

        [Test]
        public void BuildFloorPolygon_KeepsTheWindingOfTheBoundary()
        {
            var counterClockwise = new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(4f, 0f, 3f), V(0f, 0f, 3f) };
            var clockwise = Enumerable.Reverse(counterClockwise).ToArray();

            var first = FloorplanBuilder.BuildFloorPolygon(counterClockwise, null, 0f);
            var second = FloorplanBuilder.BuildFloorPolygon(clockwise, null, 0f);

            CollectionAssert.AreEqual(counterClockwise, first);
            CollectionAssert.AreEqual(clockwise, second);
            Assert.AreEqual(12f, SignedAreaXZ(first), 1e-4f);
            Assert.AreEqual(-12f, SignedAreaXZ(second), 1e-4f);
        }

        [Test]
        public void BuildFloorPolygon_KeepsAnLShapedBoundaryAsItIs()
        {
            var l = new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(4f, 0f, 2f), V(2f, 0f, 2f), V(2f, 0f, 4f), V(0f, 0f, 4f) };

            var polygon = FloorplanBuilder.BuildFloorPolygon(l, null, 0f);

            CollectionAssert.AreEqual(l, polygon);
            Assert.AreEqual(12f, SignedAreaXZ(polygon), 1e-4f, "4 x 2 plus 2 x 2");
        }

        // The boundary is not checked: duplicate and collinear points stay, so a polygon with no area comes back as it is.
        [Test]
        public void BuildFloorPolygon_Currently_KeepsDuplicateAndCollinearBoundaryPoints()
        {
            var degenerate = new[] { V(0f, 0f, 0f), V(0f, 0f, 0f), V(2f, 0f, 0f) };

            var polygon = FloorplanBuilder.BuildFloorPolygon(degenerate, null, 0f);

            CollectionAssert.AreEqual(degenerate, polygon);
            Assert.AreEqual(0f, SignedAreaXZ(polygon), 1e-6f);
        }

        [Test]
        public void BuildFloorPolygon_WithFewerThanThreeBoundaryPoints_UsesThePinsAtFloorY()
        {
            var boundary = new[] { V(0f, 0f, 0f), V(1f, 0f, 0f) };
            var pins = new[] { PinAt(0f, 1f, 0f), PinAt(4f, 2f, 0f), PinAt(0f, 3f, 3f) };

            var polygon = FloorplanBuilder.BuildFloorPolygon(boundary, pins, 0.5f);

            CollectionAssert.AreEqual(new[] { V(0f, 0.5f, 0f), V(4f, 0.5f, 0f), V(0f, 0.5f, 3f) }, polygon);
        }

        // ---- BuildFloorPolygon: from the pins (convex hull) ----

        [Test]
        public void BuildFloorPolygon_FromPins_IsTheirConvexHullAtFloorY_CounterClockwise_FromTheLowestLeftPoint()
        {
            var pins = new[]
            {
                PinAt(4f, 0.1f, 3f), PinAt(0f, 0.9f, 0f), PinAt(2f, 0.7f, 1.5f), PinAt(4f, 0.5f, 0f), PinAt(0f, 0.2f, 3f)
            };

            var polygon = FloorplanBuilder.BuildFloorPolygon(null, pins, 0.25f);

            CollectionAssert.AreEqual(new[] { V(0f, 0.25f, 0f), V(4f, 0.25f, 0f), V(4f, 0.25f, 3f), V(0f, 0.25f, 3f) }, polygon,
                "the pin inside the rectangle is not a corner, and the pins' own heights are not used");
            Assert.AreEqual(12f, SignedAreaXZ(polygon), 1e-4f);
        }

        [Test]
        public void BuildFloorPolygon_FromPins_DoesNotDependOnTheOrderOfThePins()
        {
            var pins = new[] { PinAt(0f, 0f, 0f), PinAt(4f, 0f, 0f), PinAt(4f, 0f, 3f), PinAt(0f, 0f, 3f), PinAt(2f, 0f, 1f) };

            var forward = FloorplanBuilder.BuildFloorPolygon(null, pins, 0f);
            var backward = FloorplanBuilder.BuildFloorPolygon(null, Enumerable.Reverse(pins).ToArray(), 0f);

            CollectionAssert.AreEqual(forward, backward);
        }

        // The hull of an L-shaped room is not the L: the notch is filled (area 14 instead of 12).
        [Test]
        public void BuildFloorPolygon_Currently_FillsTheNotchOfAnLShapedRoomWhenItComesFromPins()
        {
            var corners = new[] { PinAt(0f, 0f, 0f), PinAt(4f, 0f, 0f), PinAt(4f, 0f, 2f), PinAt(2f, 0f, 2f), PinAt(2f, 0f, 4f), PinAt(0f, 0f, 4f) };

            var polygon = FloorplanBuilder.BuildFloorPolygon(null, corners, 0f);

            CollectionAssert.AreEqual(new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(4f, 0f, 2f), V(2f, 0f, 4f), V(0f, 0f, 4f) }, polygon);
            Assert.AreEqual(14f, SignedAreaXZ(polygon), 1e-4f);
        }

        [Test]
        public void BuildFloorPolygon_FromThreePins_IsTheTriangle()
        {
            var pins = new[] { PinAt(0f, 0f, 3f), PinAt(4f, 0f, 0f), PinAt(0f, 0f, 0f) };

            var polygon = FloorplanBuilder.BuildFloorPolygon(null, pins, 0f);

            CollectionAssert.AreEqual(new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(0f, 0f, 3f) }, polygon);
        }

        [Test]
        public void BuildFloorPolygon_WithoutThreePinsOrABoundary_ReturnsAnEmptyList()
        {
            var empty = new List<SpatialPin>();

            AssertEmptyNotNull(FloorplanBuilder.BuildFloorPolygon(null, null, 0f));
            AssertEmptyNotNull(FloorplanBuilder.BuildFloorPolygon(new List<Vector3>(), empty, 0f));
            AssertEmptyNotNull(FloorplanBuilder.BuildFloorPolygon(null, new[] { PinAt(0f, 0f, 0f), PinAt(1f, 0f, 1f) }, 0f));
            AssertEmptyNotNull(FloorplanBuilder.BuildFloorPolygon(new[] { V(0f, 0f, 0f), V(1f, 0f, 0f) }, new[] { PinAt(0f, 0f, 0f) }, 0f));
        }

        // Pins on a line enclose no area: no polygon (a two-point "polygon" used to come back).
        [Test]
        public void BuildFloorPolygon_ReturnsAnEmptyListForCollinearPins()
        {
            var alongX = new[] { PinAt(0f, 0f, 0f), PinAt(1f, 0f, 0f), PinAt(2f, 0f, 0f) };
            var diagonal = new[] { PinAt(2f, 0f, 2f), PinAt(0f, 0f, 0f), PinAt(1f, 0f, 1f) };
            var alongZ = new[] { PinAt(1f, 0f, 0f), PinAt(1f, 0f, 1f), PinAt(1f, 0f, 2f) };

            AssertEmptyNotNull(FloorplanBuilder.BuildFloorPolygon(null, alongX, 0.25f));
            AssertEmptyNotNull(FloorplanBuilder.BuildFloorPolygon(null, diagonal, 0.25f));
            AssertEmptyNotNull(FloorplanBuilder.BuildFloorPolygon(null, alongZ, 0.25f));
        }

        // Duplicate pins do not count towards the three: pins on one or two places enclose no area.
        [Test]
        public void BuildFloorPolygon_ReturnsAnEmptyListForPinsOnOneOrTwoPlaces()
        {
            var samePlace = new[] { PinAt(1f, 0f, 1f), PinAt(1f, 0f, 1f), PinAt(1f, 0f, 1f) };
            var twoPlaces = new[] { PinAt(0f, 0f, 0f), PinAt(0f, 0f, 0f), PinAt(1f, 0f, 0f) };

            AssertEmptyNotNull(FloorplanBuilder.BuildFloorPolygon(null, samePlace, 0f));
            AssertEmptyNotNull(FloorplanBuilder.BuildFloorPolygon(null, twoPlaces, 0f));
        }

        [Test]
        public void BuildFloorPolygon_FromPins_DropsDuplicatePinsWhenThereIsARealArea()
        {
            var pins = new[] { PinAt(0f, 0f, 0f), PinAt(0f, 0f, 0f), PinAt(4f, 0f, 0f), PinAt(0f, 0f, 3f) };

            var polygon = FloorplanBuilder.BuildFloorPolygon(null, pins, 0f);

            CollectionAssert.AreEqual(new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(0f, 0f, 3f) }, polygon);
        }

        // ---- BuildFloorMesh ----

        [Test]
        public void BuildFloorMesh_ReturnsNull_WithoutAPolygonOrWithFewerThanThreePoints()
        {
            Assert.IsNull(FloorplanBuilder.BuildFloorMesh(null, null, 0f));
            Assert.IsNull(FloorplanBuilder.BuildFloorMesh(new Vector3[0], null, 0f));
            Assert.IsNull(FloorplanBuilder.BuildFloorMesh(new[] { V(0f, 0f, 0f), V(1f, 0f, 0f) }, null, 0f));
        }

        [Test]
        public void BuildFloorMesh_HasOneVertexPerPointAtFloorY_AndNoUvsOrColors()
        {
            var mesh = NewMesh(new[] { V(0f, 5f, 0f), V(4f, -2f, 0f), V(4f, 9f, 3f) }, null, 0.5f);

            CollectionAssert.AreEqual(new[] { V(0f, 0.5f, 0f), V(4f, 0.5f, 0f), V(4f, 0.5f, 3f) }, mesh.vertices);
            Assert.IsEmpty(mesh.uv);
            Assert.IsEmpty(mesh.colors);
        }

        // A convex polygon is covered by n - 2 triangles whose areas add up to its own.
        [Test]
        public void BuildFloorMesh_CoversAConvexPolygonWithNMinusTwoTriangles()
        {
            var rectangle = NewMesh(new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(4f, 0f, 3f), V(0f, 0f, 3f) }, null, 0f);
            var pentagon = NewMesh(new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(5f, 0f, 2f), V(2f, 0f, 4f), V(0f, 0f, 3f) }, null, 0f);

            AssertCoversExactly(rectangle, 12f, 2);
            AssertCoversExactly(pentagon, SignedAreaXZ(new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(5f, 0f, 2f), V(2f, 0f, 4f), V(0f, 0f, 3f) }), 3);
        }

        // A polygon that runs counterclockwise from above (as the pin hull does) used to give downward normals, so back-face
        // culling hid the floor from above. The floor faces up whichever way the polygon runs.
        [Test]
        public void BuildFloorMesh_FacesUp_WhicheverWayThePolygonRuns()
        {
            var counterClockwise = new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(4f, 0f, 3f), V(0f, 0f, 3f) };

            var first = NewMesh(counterClockwise, null, 0f);
            var second = NewMesh(Enumerable.Reverse(counterClockwise).ToArray(), null, 0f);

            foreach (var mesh in new[] { first, second })
            {
                Assert.IsNotEmpty(mesh.normals);
                foreach (var normal in mesh.normals)
                {
                    Assert.That(Vector3.Distance(normal, Vector3.up), Is.LessThan(1e-4f), "normal " + normal);
                }
            }
        }

        [Test]
        public void BuildFloorMesh_PlacesTheVerticesInTheLocalSpaceOfTheRoot()
        {
            var root = NewRoot(V(10f, 0f, 20f), 2f);

            var mesh = NewMesh(new[] { V(10f, 0f, 20f), V(14f, 0f, 20f), V(14f, 0f, 26f) }, root, 1f);

            var expected = new[] { V(0f, 0.5f, 0f), V(2f, 0.5f, 0f), V(2f, 0.5f, 3f) };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(Vector3.Distance(expected[i], mesh.vertices[i]), Is.LessThan(1e-4f), "vertex " + i + ": " + mesh.vertices[i]);
            }
        }

        // A fan from the first point turned one triangle of a U-shaped boundary the other way, over part of the notch. The
        // triangles now cover the polygon exactly: all face up and their areas add up to its own (24 less the notch of 4).
        [Test]
        public void BuildFloorMesh_CoversAUShapedPolygonExactly_WithNoTriangleTurnedTheOtherWay()
        {
            var u = new[]
            {
                V(0f, 0f, 0f), V(6f, 0f, 0f), V(6f, 0f, 4f), V(4f, 0f, 4f), V(4f, 0f, 2f), V(2f, 0f, 2f), V(2f, 0f, 4f), V(0f, 0f, 4f)
            };

            AssertCoversExactly(NewMesh(u, null, 0f), 20f, 6);
            AssertCoversExactly(NewMesh(Enumerable.Reverse(u).ToArray(), null, 0f), 20f, 6);
        }

        [Test]
        public void BuildFloorMesh_CoversAnLShapedPolygonExactly()
        {
            var l = new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(4f, 0f, 2f), V(2f, 0f, 2f), V(2f, 0f, 4f), V(0f, 0f, 4f) };

            AssertCoversExactly(NewMesh(l, null, 0f), 12f, 4);
        }

        // A point in the middle of an edge (what a boundary traced with many points has) adds no triangle and no area.
        [Test]
        public void BuildFloorMesh_CoversAPolygonWithPointsOnItsEdgesExactly()
        {
            var rectangle = new[] { V(0f, 0f, 0f), V(2f, 0f, 0f), V(4f, 0f, 0f), V(4f, 0f, 3f), V(0f, 0f, 3f) };

            var mesh = NewMesh(rectangle, null, 0f);

            Assert.AreEqual(5, mesh.vertexCount, "every point is a vertex");
            AssertCoversExactly(mesh, 12f, 2);
        }

        [Test]
        public void BuildFloorMesh_OfTheSameWholePolygonTwice_GivesTheSameTriangles()
        {
            var l = new[] { V(0f, 0f, 0f), V(4f, 0f, 0f), V(4f, 0f, 2f), V(2f, 0f, 2f), V(2f, 0f, 4f), V(0f, 0f, 4f) };

            CollectionAssert.AreEqual(NewMesh(l, null, 0f).triangles, NewMesh(l, null, 0f).triangles);
        }

        // ---- ComputeBounds ----

        [Test]
        public void ComputeBounds_WithNothing_IsAnEmptyBoundsAtTheOrigin()
        {
            foreach (var bounds in new[]
                     {
                         FloorplanBuilder.ComputeBounds(null, null),
                         FloorplanBuilder.ComputeBounds(new List<Vector3>(), new List<SpatialPin>())
                     })
            {
                Assert.AreEqual(Vector3.zero, bounds.center);
                Assert.AreEqual(Vector3.zero, bounds.size);
            }
        }

        [Test]
        public void ComputeBounds_CoversTheBoundaryAndThePinsInThreeDimensions()
        {
            var boundary = new[] { V(0f, 0f, 0f), V(4f, 0f, 3f) };
            var pins = new[] { PinAt(5f, 2f, -1f), PinAt(1f, -1f, 1f) };

            var bounds = FloorplanBuilder.ComputeBounds(boundary, pins);

            Assert.AreEqual(V(2.5f, 0.5f, 1f), bounds.center);
            Assert.AreEqual(V(5f, 3f, 4f), bounds.size);
        }

        [Test]
        public void ComputeBounds_OfPinsAlone_UsesTheirPositions_AndOfOnePinHasNoSize()
        {
            var two = FloorplanBuilder.ComputeBounds(null, new[] { PinAt(1f, 2f, 3f), PinAt(3f, 4f, 5f) });
            var one = FloorplanBuilder.ComputeBounds(null, new[] { PinAt(1f, 2f, 3f) });

            Assert.AreEqual(V(2f, 3f, 4f), two.center);
            Assert.AreEqual(V(2f, 2f, 2f), two.size);
            Assert.AreEqual(V(1f, 2f, 3f), one.center);
            Assert.AreEqual(Vector3.zero, one.size);
        }

        [Test]
        public void ComputeBounds_OfABoundaryAlone_UsesItsPoints_EvenWithFewerThanThree()
        {
            var bounds = FloorplanBuilder.ComputeBounds(new[] { V(-1f, 0f, -1f), V(1f, 0f, 1f) }, null);

            Assert.AreEqual(Vector3.zero, bounds.center);
            Assert.AreEqual(V(2f, 0f, 2f), bounds.size);
        }

        private static Vector3 V(float x, float y, float z)
        {
            return new Vector3(x, y, z);
        }

        private static SpatialPin PinAt(float x, float y, float z)
        {
            return new SpatialPin { position = new Vector3(x, y, z) };
        }

        /// <summary>Signed area in the x-z plane: positive when counterclockwise seen from above.</summary>
        private static float SignedAreaXZ(IReadOnlyList<Vector3> polygon)
        {
            float twice = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                twice += a.x * b.z - b.x * a.z;
            }

            return twice * 0.5f;
        }

        /// <summary>
        /// The mesh's triangles all face up (a positive area seen from above) and add up to <paramref name="polygonArea"/>
        /// (which may be negative for a polygon that runs clockwise: only its size counts), in the stated number of triangles.
        /// </summary>
        private static void AssertCoversExactly(Mesh mesh, float polygonArea, int triangleCount)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var areas = new List<float>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // SignedAreaXZ is positive counterclockwise; a triangle that faces up runs clockwise, so its area here is negative.
                areas.Add(-SignedAreaXZ(new[] { vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]] }));
            }

            Assert.AreEqual(triangleCount, areas.Count, "triangles: " + string.Join(", ", areas));
            Assert.IsTrue(areas.All(area => area > 0f), "a triangle faces down or has no area: " + string.Join(", ", areas));
            Assert.AreEqual(Mathf.Abs(polygonArea), areas.Sum(), 1e-4f, "the triangles do not cover the polygon exactly");
        }

        private static void AssertEmptyNotNull(List<Vector3> polygon)
        {
            Assert.IsNotNull(polygon);
            Assert.IsEmpty(polygon);
        }

        private Mesh NewMesh(IReadOnlyList<Vector3> polygon, Transform root, float floorY)
        {
            var mesh = FloorplanBuilder.BuildFloorMesh(polygon, root, floorY);
            Assert.IsNotNull(mesh);
            _created.Add(mesh);
            return mesh;
        }

        private Transform NewRoot(Vector3 position, float scale)
        {
            var go = new GameObject("FloorplanRoot");
            _created.Add(go);
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale, scale, scale);
            return go.transform;
        }
    }
}
