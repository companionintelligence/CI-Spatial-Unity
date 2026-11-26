using System.Collections.Generic;
using System.Reflection;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// Edge cases that the PlayMode ViewportRaycasterTests (a ray from the intrinsics and the pose,
    /// a pixel offset, the fallback point) do not cover. A test with "Currently" in its name
    /// records a result that looks doubtful, not a requirement. The raycasts start far from the
    /// origin and point away from it, so that nothing in an open scene can be hit.
    /// </summary>
    public class ViewportRaycasterCharacterizationTests
    {
        private static readonly Vector3 Far = new Vector3(1000f, 2000f, 3000f);

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

        // ---- BuildRayFromIntrinsics ----

        [Test]
        public void BuildRayFromIntrinsics_WithoutAFrame_IsAForwardRayFromTheOrigin()
        {
            var ray = ViewportRaycaster.BuildRayFromIntrinsics(null, new Vector2(10f, 20f));

            Assert.AreEqual(Vector3.zero, ray.origin);
            AssertDirection(Vector3.forward, ray);
        }

        [TestCase(10f, 20f)]
        [TestCase(500f, 400f)]
        public void BuildRayFromIntrinsics_WithoutIntrinsics_FollowsThePoseAndIgnoresThePixel(float x, float y)
        {
            var frame = new CaptureFrame
            {
                cameraPose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f)),
                cameraIntrinsics = Matrix4x4.zero
            };

            var ray = ViewportRaycaster.BuildRayFromIntrinsics(frame, new Vector2(x, y));

            Assert.AreEqual(new Vector3(1f, 2f, 3f), ray.origin);
            AssertDirection(Vector3.right, ray);
        }

        [Test]
        public void BuildRayFromIntrinsics_StartsAtThePosesPosition_AndTheDirectionHasUnitLength()
        {
            var frame = Frame(new Vector3(4f, 5f, 6f), Quaternion.Euler(10f, 20f, 30f), 100f, 100f, 50f, 50f);

            var ray = ViewportRaycaster.BuildRayFromIntrinsics(frame, new Vector2(7f, 93f));

            Assert.AreEqual(new Vector3(4f, 5f, 6f), ray.origin);
            Assert.That(ray.direction.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        // Pixel rows count down from the top, so a pixel below the principal point looks down.
        [Test]
        public void BuildRayFromIntrinsics_ALowerPixelRowGivesARayThatPointsLower()
        {
            var frame = Frame(Vector3.zero, Quaternion.identity, 100f, 100f, 50f, 50f);

            AssertDirection(new Vector3(0f, -0.1f, 1f), ViewportRaycaster.BuildRayFromIntrinsics(frame, new Vector2(50f, 60f)));
            AssertDirection(new Vector3(0f, 0.1f, 1f), ViewportRaycaster.BuildRayFromIntrinsics(frame, new Vector2(50f, 40f)));
        }

        [Test]
        public void BuildRayFromIntrinsics_DividesEachAxisByItsOwnFocalLength()
        {
            var frame = Frame(Vector3.zero, Quaternion.identity, 200f, 100f, 50f, 50f);

            AssertDirection(new Vector3(0.1f, -0.1f, 1f), ViewportRaycaster.BuildRayFromIntrinsics(frame, new Vector2(70f, 60f)));
        }

        [Test]
        public void BuildRayFromIntrinsics_TurnsTheCameraDirectionByThePosesRotation()
        {
            var frame = Frame(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), 100f, 100f, 50f, 50f);

            AssertDirection(Vector3.down, ViewportRaycaster.BuildRayFromIntrinsics(frame, new Vector2(50f, 50f)));
        }

        // The identity matrix is not intrinsics (a focal length of one pixel would give the middle of a 640 x 480 image a nearly sideways ray): the ray follows the pose.
        [Test]
        public void BuildRayFromIntrinsics_TreatsTheIdentityMatrixAsNoIntrinsics_AndFollowsThePose()
        {
            var frame = new CaptureFrame
            {
                cameraPose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f)),
                cameraIntrinsics = Matrix4x4.identity
            };

            var ray = ViewportRaycaster.BuildRayFromIntrinsics(frame, new Vector2(320f, 240f));

            Assert.AreEqual(new Vector3(1f, 2f, 3f), ray.origin);
            AssertDirection(Vector3.right, ray);
        }

        // ---- TryRaycastCapture ----

        [Test]
        public void TryRaycastCapture_WithoutFallback_ReturnsFalseAndLeavesThePointAtTheOriginWithAnUpwardNormal()
        {
            var raycaster = NewRaycaster();
            var frame = new CaptureFrame { cameraPose = new Pose(Far, Quaternion.identity), cameraIntrinsics = Matrix4x4.zero };

            var hit = raycaster.TryRaycastCapture(frame, Vector2.zero, false, out var point, out var normal, out var hitType);

            Assert.IsFalse(hit);
            Assert.AreEqual(Vector3.zero, point);
            Assert.AreEqual(Vector3.up, normal);
            Assert.AreEqual(RaycastHitType.Fallback, hitType);
        }

        [Test]
        public void TryRaycastCapture_WithFallback_PutsThePointOneFallbackDistanceAlongTheRay_AndStillReturnsFalse()
        {
            var raycaster = NewRaycaster();
            raycaster.FallbackDistance = 3f;
            var frame = new CaptureFrame { cameraPose = new Pose(Far, Quaternion.Euler(0f, 90f, 0f)), cameraIntrinsics = Matrix4x4.zero };

            var hit = raycaster.TryRaycastCapture(frame, Vector2.zero, true, out var point, out var normal, out var hitType);
            var shortOverload = raycaster.TryRaycastCapture(frame, Vector2.zero, out var shortPoint, out _, out _);

            Assert.IsFalse(hit);
            Assert.IsFalse(shortOverload);
            Assert.That(Vector3.Distance(Far + Vector3.right * 3f, point), Is.LessThan(1e-3f));
            Assert.AreEqual(point, shortPoint, "the overload without allowFallback allows it");
            Assert.AreEqual(Vector3.up, normal);
            Assert.AreEqual(RaycastHitType.Fallback, hitType);
        }

        // No frame means no ray (the pixel belongs to a frame): no hit and no fallback point, whatever the pixel and allowFallback. It used to raycast through the bottom left corner of the camera's view.
        [Test]
        public void TryRaycastCapture_WithoutAFrame_HasNoRayAndNoHit_WhateverThePixelAndAllowFallback()
        {
            var camera = NewCamera(Far, Quaternion.identity);
            var raycaster = NewRaycaster(camera);

            var withFallback = raycaster.TryRaycastCapture(null, new Vector2(300f, 200f), true, out var first, out var firstNormal, out var firstType);
            var withoutFallback = raycaster.TryRaycastCapture(null, new Vector2(10f, 10f), false, out var second, out var secondNormal, out var secondType);

            Assert.IsFalse(withFallback);
            Assert.IsFalse(withoutFallback);
            Assert.AreEqual(Vector3.zero, first);
            Assert.AreEqual(Vector3.zero, second);
            Assert.AreEqual(Vector3.up, firstNormal);
            Assert.AreEqual(Vector3.up, secondNormal);
            Assert.AreEqual(RaycastHitType.Fallback, firstType);
            Assert.AreEqual(RaycastHitType.Fallback, secondType);
        }

        [TestCase(-2f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(2.5f, 2.5f)]
        public void FallbackDistance_CannotBeSetBelowZero(float value, float expected)
        {
            var raycaster = NewRaycaster();

            raycaster.FallbackDistance = value;

            Assert.AreEqual(expected, raycaster.FallbackDistance);
        }

        [Test]
        public void FallbackDistance_IsTwoMetresUntilItIsSet()
        {
            Assert.AreEqual(2f, NewRaycaster().FallbackDistance);
        }

        private ViewportRaycaster NewRaycaster(Camera camera = null)
        {
            var go = new GameObject("ViewportRaycaster");
            _created.Add(go);
            var raycaster = go.AddComponent<ViewportRaycaster>();
            if (camera != null)
            {
                SetField(raycaster, "targetCamera", camera);
            }

            return raycaster;
        }

        private Camera NewCamera(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("TargetCamera");
            _created.Add(go);
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.aspect = 2f;
            go.transform.SetPositionAndRotation(position, rotation);
            return camera;
        }

        private static CaptureFrame Frame(Vector3 position, Quaternion rotation, float fx, float fy, float cx, float cy)
        {
            var intrinsics = Matrix4x4.zero;
            intrinsics.m00 = fx;
            intrinsics.m11 = fy;
            intrinsics.m02 = cx;
            intrinsics.m12 = cy;
            return new CaptureFrame
            {
                imageWidth = 100,
                imageHeight = 100,
                cameraPose = new Pose(position, rotation),
                cameraIntrinsics = intrinsics
            };
        }

        private static void AssertDirection(Vector3 expectedDirection, Ray ray)
        {
            var expected = expectedDirection.normalized;
            Assert.That(Vector3.Distance(expected, ray.direction), Is.LessThan(1e-4f), $"expected {expected} but was {ray.direction}");
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }
    }
}
