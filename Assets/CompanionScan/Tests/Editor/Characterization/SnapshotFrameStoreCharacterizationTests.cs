using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// Characterization tests: they record what <see cref="SnapshotFrameStore"/> does today. A test
    /// with "Currently" in its name records a result that looks doubtful, not a requirement. The
    /// PlayMode SnapshotFrameStoreTests has the one basic test (a frame and four corners are
    /// stored); these cover the rest.
    /// </summary>
    public class SnapshotFrameStoreCharacterizationTests
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

        // ---- ids ----

        [Test]
        public void AddSnapshot_ReturnsTheIdOfTheStoredFrame_ThePrefixAndAFourDigitCounterFromOne()
        {
            var store = NewStore();

            var first = store.AddSnapshot(Capture(), 1f, null);
            var second = store.AddSnapshot(Capture(), 1f, null);
            var third = store.AddSnapshot(Capture(), 1f, null);

            CollectionAssert.AreEqual(new[] { "snap_0001", "snap_0002", "snap_0003" }, new[] { first, second, third });
            CollectionAssert.AreEqual(new[] { first, second, third }, store.Frames.Select(frame => frame.id).ToArray());
        }

        [Test]
        public void AddSnapshot_UsesThePrefixOfTheStore()
        {
            var store = NewStore(idPrefix: "scan");

            Assert.AreEqual("scan_0001", store.AddSnapshot(Capture(), 1f, null));
        }

        [Test]
        public void AddSnapshot_KeepsCountingAfterClearAndConsumeFrames_SoAnIdIsNeverUsedTwice()
        {
            var store = NewStore();

            Assert.AreEqual("snap_0001", store.AddSnapshot(Capture(), 1f, null));
            store.Clear();
            Assert.AreEqual("snap_0002", store.AddSnapshot(Capture(), 1f, null));
            store.ConsumeFrames();
            Assert.AreEqual("snap_0003", store.AddSnapshot(Capture(), 1f, null));
        }

        [Test]
        public void AddSnapshot_NumbersPastNineThousandNineHundredNinetyNineWithFiveDigitsAndDoesNotWrap()
        {
            var store = NewStore();
            SetField(store, "_counter", 9998);

            Assert.AreEqual("snap_9999", store.AddSnapshot(Capture(), 1f, null));
            Assert.AreEqual("snap_10000", store.AddSnapshot(Capture(), 1f, null));
        }

        // ---- what is stored ----

        [Test]
        public void AddSnapshot_StoresTheCapturesImagePoseIntrinsicsAndTime_AndTheDistanceAsGiven()
        {
            var store = NewStore();
            var capture = Capture(timestamp: 10.0);

            var id = store.AddSnapshot(capture, 2.5f, null);

            Assert.AreEqual(1, store.Frames.Count);
            var frame = store.Frames[0];
            Assert.AreEqual(id, frame.id);
            Assert.AreEqual(10.0, frame.timestamp);
            Assert.AreSame(capture.jpegBytes, frame.jpegBytes, "the bytes are not copied");
            Assert.AreEqual(640, frame.imageWidth);
            Assert.AreEqual(480, frame.imageHeight);
            Assert.AreEqual(capture.cameraPose.position, frame.cameraPosition);
            Assert.AreEqual(capture.cameraPose.rotation, frame.cameraRotation);
            Assert.AreEqual(2.5f, frame.frustumDistance);
            Assert.AreEqual(capture.cameraIntrinsics, frame.cameraIntrinsics);
        }

        [TestCase(0.0)]
        [TestCase(-3.0)]
        public void AddSnapshot_UsesTheCurrentTimeWhenTheCapturesTimeIsNotAboveZero(double captureTime)
        {
            var store = NewStore();

            var before = Time.timeAsDouble;
            store.AddSnapshot(Capture(timestamp: captureTime), 1f, null);
            var after = Time.timeAsDouble;

            Assert.That(store.Frames[0].timestamp, Is.InRange(before, after));
        }

        [Test]
        public void AddSnapshot_WithoutAnImage_StoresNothing_ReturnsNull_AndDoesNotUseUpAnId()
        {
            var store = NewStore();

            Assert.IsNull(store.AddSnapshot(null, 1f, null));
            Assert.IsNull(store.AddSnapshot(new CaptureFrame(), 1f, null), "no jpegBytes");
            Assert.IsNull(store.AddSnapshot(Capture(jpeg: new byte[0]), 1f, null), "empty jpegBytes");
            Assert.AreEqual(0, store.Frames.Count);

            Assert.AreEqual("snap_0001", store.AddSnapshot(Capture(), 1f, null));
        }

        // ---- frustum corners ----

        [Test]
        public void AddSnapshot_StoresTheFourCornersOfTheCamerasViewAtTheDistance_BottomLeftTopLeftTopRightBottomRight()
        {
            var camera = NewCamera(new Vector3(1f, 2f, 3f), Quaternion.identity, 90f, 2f);
            var store = NewStore();

            store.AddSnapshot(Capture(), 2f, camera);

            // 2 in front of the camera a 90 degree vertical field of view is 4 high, and aspect 2 makes it 8 wide.
            AssertCorners(new[] { new Vector3(-3f, 0f, 5f), new Vector3(-3f, 4f, 5f), new Vector3(5f, 4f, 5f), new Vector3(5f, 0f, 5f) },
                store.Frames[0].frustumCorners);
        }

        [Test]
        public void AddSnapshot_PlacesTheCornersOnAPlaneInFrontOfTheCamera_WhateverItsRotationAndFieldOfView()
        {
            var camera = NewCamera(new Vector3(-2f, 1f, 4f), Quaternion.Euler(20f, 130f, 0f), 60f, 1.5f);
            var store = NewStore();

            store.AddSnapshot(Capture(), 3f, camera);

            var transform = camera.transform;
            var centre = transform.position + transform.forward * 3f;
            var up = transform.up * (3f * Mathf.Tan(30f * Mathf.Deg2Rad));
            var right = transform.right * (3f * Mathf.Tan(30f * Mathf.Deg2Rad) * 1.5f);
            AssertCorners(new[] { centre - right - up, centre - right + up, centre + right + up, centre + right - up },
                store.Frames[0].frustumCorners);
        }

        // The corners come from the scene camera passed in, not from the capture's pose or intrinsics, so they can disagree with the stored pose.
        [Test]
        public void AddSnapshot_Currently_TakesTheCornersFromTheSceneCameraNotFromTheCapturesPoseOrIntrinsics()
        {
            var camera = NewCamera(new Vector3(1f, 2f, 3f), Quaternion.identity, 90f, 2f);
            var store = NewStore();
            var elsewhere = new Pose(new Vector3(50f, 60f, 70f), Quaternion.Euler(0f, 180f, 0f));

            store.AddSnapshot(Capture(pose: new Pose(camera.transform.position, camera.transform.rotation), intrinsics: Matrix4x4.zero), 2f, camera);
            store.AddSnapshot(Capture(pose: elsewhere, intrinsics: Intrinsics(900f, 900f, 100f, 100f)), 2f, camera);

            var frames = store.Frames;
            AssertCorners(frames[0].frustumCorners, frames[1].frustumCorners);
            Assert.AreEqual(elsewhere.position, frames[1].cameraPosition, "the stored pose is still the capture's");
        }

        [Test]
        public void AddSnapshot_WithoutACameraOrWithADistanceNotAboveZero_StoresTheFrameWithAnEmptyCornerList()
        {
            var camera = NewCamera(Vector3.zero, Quaternion.identity, 90f, 2f);
            var destroyed = NewCamera(Vector3.zero, Quaternion.identity, 90f, 2f);
            Object.DestroyImmediate(destroyed);
            var store = NewStore();

            store.AddSnapshot(Capture(), 1f, null);
            store.AddSnapshot(Capture(), 0f, camera);
            store.AddSnapshot(Capture(), -2f, camera);
            store.AddSnapshot(Capture(), 1f, destroyed);

            Assert.AreEqual(4, store.Frames.Count);
            foreach (var frame in store.Frames)
            {
                Assert.IsNotNull(frame.frustumCorners);
                Assert.IsEmpty(frame.frustumCorners);
            }

            CollectionAssert.AreEqual(new[] { 1f, 0f, -2f, 1f }, store.Frames.Select(frame => frame.frustumDistance).ToArray(),
                "the distance is stored as given, even when no corners are made from it");
        }

        // ---- limit ----

        [Test]
        public void AddSnapshot_KeepsTheNewestFiftyFramesByDefault()
        {
            var store = NewStore();

            for (int i = 0; i < 51; i++)
            {
                store.AddSnapshot(Capture(), 1f, null);
            }

            Assert.AreEqual(50, store.Frames.Count);
            Assert.AreEqual("snap_0002", store.Frames[0].id);
            Assert.AreEqual("snap_0051", store.Frames[49].id);
        }

        // The oldest snapshot is dropped silently, so a pin's capture id can name a snapshot that is never exported.
        [Test]
        public void AddSnapshot_Currently_DropsTheOldestFrameSilentlyOnceTheLimitIsPassed()
        {
            var store = NewStore(maxFrames: 3);

            var ids = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                ids.Add(store.AddSnapshot(Capture(), 1f, null));
            }

            CollectionAssert.AreEqual(new[] { "snap_0001", "snap_0002", "snap_0003", "snap_0004", "snap_0005" }, ids,
                "the id of a frame that is dropped later is still returned");
            CollectionAssert.AreEqual(new[] { "snap_0003", "snap_0004", "snap_0005" }, store.Frames.Select(frame => frame.id).ToArray());
        }

        [TestCase(0)]
        [TestCase(-4)]
        public void AddSnapshot_WithALimitBelowOne_KeepsOnlyTheNewestFrame(int limit)
        {
            var store = NewStore(maxFrames: limit);

            store.AddSnapshot(Capture(), 1f, null);
            store.AddSnapshot(Capture(), 1f, null);
            store.AddSnapshot(Capture(), 1f, null);

            CollectionAssert.AreEqual(new[] { "snap_0003" }, store.Frames.Select(frame => frame.id).ToArray());
        }

        // ---- reading and emptying ----

        [Test]
        public void ConsumeFrames_ReturnsTheFramesOldestFirstAsACopy_AndEmptiesTheStore()
        {
            var store = NewStore();
            store.AddSnapshot(Capture(), 1f, null);
            store.AddSnapshot(Capture(), 1f, null);
            store.AddSnapshot(Capture(), 1f, null);

            var consumed = store.ConsumeFrames();
            store.AddSnapshot(Capture(), 1f, null);

            CollectionAssert.AreEqual(new[] { "snap_0001", "snap_0002", "snap_0003" }, consumed.Select(frame => frame.id).ToArray());
            CollectionAssert.AreEqual(new[] { "snap_0004" }, store.Frames.Select(frame => frame.id).ToArray());
        }

        [Test]
        public void ConsumeFrames_OfAnEmptyStore_ReturnsAnEmptyListNotNull()
        {
            var consumed = NewStore().ConsumeFrames();

            Assert.IsNotNull(consumed);
            Assert.IsEmpty(consumed);
        }

        [Test]
        public void Clear_RemovesEveryFrame()
        {
            var store = NewStore();
            store.AddSnapshot(Capture(), 1f, null);
            store.AddSnapshot(Capture(), 1f, null);

            store.Clear();

            Assert.AreEqual(0, store.Frames.Count);
        }

        [Test]
        public void Frames_IsALiveViewOfTheStore_NotACopy()
        {
            var store = NewStore();
            var view = store.Frames;

            store.AddSnapshot(Capture(), 1f, null);

            Assert.AreEqual(1, view.Count);
            store.Clear();
            Assert.AreEqual(0, view.Count);
        }

        private SnapshotFrameStore NewStore(int maxFrames = 50, string idPrefix = "snap")
        {
            var go = new GameObject("SnapshotFrameStore");
            _created.Add(go);
            var store = go.AddComponent<SnapshotFrameStore>();
            SetField(store, "maxFrames", maxFrames);
            SetField(store, "idPrefix", idPrefix);
            return store;
        }

        private Camera NewCamera(Vector3 position, Quaternion rotation, float fieldOfView, float aspect)
        {
            var go = new GameObject("SnapshotCamera");
            _created.Add(go);
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = fieldOfView;
            camera.aspect = aspect;
            go.transform.SetPositionAndRotation(position, rotation);
            return camera;
        }

        private static CaptureFrame Capture(double timestamp = 10.0, byte[] jpeg = null, Pose? pose = null, Matrix4x4? intrinsics = null)
        {
            return new CaptureFrame
            {
                timestamp = timestamp,
                jpegBytes = jpeg ?? new byte[] { 1, 2, 3, 4 },
                imageWidth = 640,
                imageHeight = 480,
                cameraPose = pose ?? new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f)),
                cameraIntrinsics = intrinsics ?? Intrinsics(500f, 500f, 320f, 240f)
            };
        }

        private static Matrix4x4 Intrinsics(float fx, float fy, float cx, float cy)
        {
            var matrix = Matrix4x4.zero;
            matrix.m00 = fx;
            matrix.m11 = fy;
            matrix.m02 = cx;
            matrix.m12 = cy;
            matrix.m22 = 1f;
            return matrix;
        }

        private static void AssertCorners(Vector3[] expected, Vector3[] actual)
        {
            Assert.AreEqual(4, actual.Length, "corners");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(Vector3.Distance(expected[i], actual[i]), Is.LessThan(1e-3f), $"corner {i}: expected {expected[i]} but was {actual[i]}");
            }
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }
    }
}
