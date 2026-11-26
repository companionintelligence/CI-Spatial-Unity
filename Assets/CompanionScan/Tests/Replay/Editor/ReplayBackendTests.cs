using System.Collections.Generic;
using System.Linq;
using CompanionScan.Core;
using CompanionScan.Mapping;
using CompanionScan.Replay;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Replay.Tests
{
    /// <summary>
    /// The replay backends one by one. A recorded session replaces the headset's camera, scene read and
    /// raycast, so these are the pieces the pipeline test stands on.
    /// </summary>
    public class ReplayBackendTests
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

        // ---- the synthetic room ----

        [Test]
        public void TheSyntheticRoom_HasOneFramePerHeading_SixPlanesAndSixSceneAnchors()
        {
            var recording = SyntheticRoom.Build(0f, 90f, 180f);

            Assert.AreEqual(3, recording.frames.Count);
            Assert.AreEqual(6, recording.planes.Count);
            Assert.AreEqual(6, recording.scene.anchors.Count);
            CollectionAssert.AreEqual(new[] { 10.0, 11.0, 12.0 }, recording.frames.Select(frame => frame.timestamp).ToArray());
            Assert.AreEqual("synthetic-room", recording.scene.primaryRoomUuid);
        }

        [Test]
        public void TheSyntheticRoom_DefaultsToTwoFrames()
        {
            Assert.AreEqual(2, SyntheticRoom.Build().frames.Count);
        }

        [Test]
        public void TheSyntheticFrames_AreRealJpegsOfTheStatedSize()
        {
            var frame = SyntheticRoom.Build(0f).frames[0];
            var texture = new Texture2D(2, 2);
            _created.Add(texture);

            Assert.IsTrue(texture.LoadImage(frame.jpeg), "not a JPEG the engine can decode");

            Assert.AreEqual(SyntheticRoom.ImageWidth, texture.width);
            Assert.AreEqual(SyntheticRoom.ImageHeight, texture.height);
            Assert.AreEqual(frame.width, texture.width);
            Assert.AreEqual(frame.height, texture.height);
        }

        [Test]
        public void TheSyntheticFrames_LookAlongTheirHeading_FromEyeHeightInTheMiddleOfTheRoom()
        {
            var recording = SyntheticRoom.Build(0f, 90f, 180f, 270f);

            AssertLooks(Vector3.forward, recording.frames[0]);
            AssertLooks(Vector3.right, recording.frames[1]);
            AssertLooks(Vector3.back, recording.frames[2]);
            AssertLooks(Vector3.left, recording.frames[3]);
            foreach (var frame in recording.frames)
            {
                Assert.AreEqual(new Vector3(0f, SyntheticRoom.EyeHeight, 0f), frame.pose.position);
                Assert.IsTrue(new CaptureFrame { cameraIntrinsics = frame.intrinsics }.HasIntrinsics);
            }
        }

        // The geometry a test works out by hand: the middle of frame 0 meets the far wall straight ahead.
        [Test]
        public void ThePixelInTheMiddleOfAFrame_MeetsTheWallItLooksAt()
        {
            var recording = SyntheticRoom.Build(0f, 90f);
            var raycast = new ReplayRaycastSource(recording.planes);

            Assert.IsTrue(raycast.TryRaycast(RayThrough(recording.frames[0], 320f, 240f), 10f, out var ahead));
            Assert.IsTrue(raycast.TryRaycast(RayThrough(recording.frames[1], 320f, 240f), 10f, out var right));

            AssertNear(new Vector3(0f, 1.5f, 3f), ahead.Point);
            AssertNear(Vector3.back, ahead.Normal);
            AssertNear(new Vector3(2f, 1.5f, 0f), right.Point);
            AssertNear(Vector3.left, right.Normal);
        }

        // 100 pixels right of the middle with a 500 pixel focal length is 0.2 of the distance to the right (0.6 m at 3 m);
        // 50 pixels above the middle is 0.1 up (0.3 m at 3 m).
        [Test]
        public void APixelRightOfTheMiddle_MeetsTheWallToTheRight_AndOneAboveItMeetsTheWallHigher()
        {
            var recording = SyntheticRoom.Build(0f);
            var raycast = new ReplayRaycastSource(recording.planes);

            Assert.IsTrue(raycast.TryRaycast(RayThrough(recording.frames[0], 420f, 240f), 10f, out var right));
            Assert.IsTrue(raycast.TryRaycast(RayThrough(recording.frames[0], 320f, 190f), 10f, out var above));

            AssertNear(new Vector3(0.6f, 1.5f, 3f), right.Point);
            AssertNear(new Vector3(0f, 1.5f + 0.3f, 3f), above.Point);
        }

        // ---- the raycast backend ----

        [Test]
        public void TheRaycastSource_FindsTheFloorBelowAndTheCeilingAbove()
        {
            var raycast = new ReplayRaycastSource(SyntheticRoom.Build().planes);
            var origin = new Vector3(0.5f, 1.5f, 0.5f);

            Assert.IsTrue(raycast.TryRaycast(new Ray(origin, Vector3.down), 10f, out var floor));
            Assert.IsTrue(raycast.TryRaycast(new Ray(origin, Vector3.up), 10f, out var ceiling));

            AssertNear(new Vector3(0.5f, 0f, 0.5f), floor.Point);
            AssertNear(Vector3.up, floor.Normal);
            AssertNear(new Vector3(0.5f, 2.6f, 0.5f), ceiling.Point);
            AssertNear(Vector3.down, ceiling.Normal);
            Assert.AreEqual(RaycastHitType.Environment, floor.Type, "it stands in for the headset's environment raycast");
        }

        [Test]
        public void TheRaycastSource_TakesTheNearestPlane()
        {
            var raycast = new ReplayRaycastSource(SyntheticRoom.Build().planes);
            // From (1.5, 1.5, 0) going towards +x and +z equally: the wall at x = 2 is 0.5 away along x, the wall at z = 3 is 3 away.
            var ray = new Ray(new Vector3(1.5f, 1.5f, 0f), new Vector3(1f, 0f, 1f));

            Assert.IsTrue(raycast.TryRaycast(ray, 10f, out var hit));

            AssertNear(new Vector3(2f, 1.5f, 0.5f), hit.Point);
            AssertNear(Vector3.left, hit.Normal);
        }

        [Test]
        public void TheRaycastSource_HasNoHitBeyondTheMaximumDistance()
        {
            var raycast = new ReplayRaycastSource(SyntheticRoom.Build().planes);
            var ray = new Ray(new Vector3(0f, 1.5f, 0f), Vector3.forward);

            Assert.IsFalse(raycast.TryRaycast(ray, 2.9f, out _), "the wall is 3 m away");
            Assert.IsTrue(raycast.TryRaycast(ray, 3.1f, out _));
        }

        [Test]
        public void TheRaycastSource_IgnoresAPlaneThatTheRayLeavesOrRunsAlong()
        {
            var raycast = new ReplayRaycastSource(new List<ReplayPlane>
            {
                new ReplayPlane { point = new Vector3(0f, 0f, 3f), normal = Vector3.back }
            });

            Assert.IsFalse(raycast.TryRaycast(new Ray(Vector3.zero, Vector3.back), 10f, out _), "walking away from the wall");
            Assert.IsFalse(raycast.TryRaycast(new Ray(Vector3.zero, Vector3.right), 10f, out _), "running along the wall");
            Assert.IsFalse(raycast.TryRaycast(new Ray(new Vector3(0f, 0f, 5f), Vector3.forward), 10f, out _), "from behind the wall");
        }

        [Test]
        public void TheRaycastSource_WithNoPlanes_HasNoHit()
        {
            Assert.IsFalse(new ReplayRaycastSource(null).TryRaycast(new Ray(Vector3.zero, Vector3.forward), 10f, out _));
            Assert.IsFalse(new ReplayRaycastSource(new List<ReplayPlane>()).TryRaycast(new Ray(Vector3.zero, Vector3.forward), 10f, out _));
        }

        // ---- the camera backend ----

        [Test]
        public void TheCamera_GivesTheRecordedFramesInOrder_WithTheirOwnPoseIntrinsicsTimeAndSize_ThenNone()
        {
            var recording = SyntheticRoom.Build(0f, 90f);
            var camera = NewCamera(recording);

            Assert.IsTrue(camera.TryCaptureFrame(75, out var first));
            Assert.IsTrue(camera.TryCaptureFrame(75, out var second));
            Assert.IsFalse(camera.TryCaptureFrame(75, out _), "the recording is over");

            Assert.AreSame(recording.frames[0].jpeg, first.Jpeg);
            Assert.AreEqual(recording.frames[0].pose, first.Pose);
            Assert.AreEqual(recording.frames[1].pose, second.Pose);
            Assert.AreEqual(recording.frames[1].intrinsics, second.Intrinsics);
            Assert.AreEqual(10.0, first.Timestamp);
            Assert.AreEqual(11.0, second.Timestamp);
            Assert.AreEqual(SyntheticRoom.ImageWidth, first.Width);
            Assert.AreEqual(SyntheticRoom.ImageHeight, first.Height);
            Assert.IsTrue(first.PoseIsGuaranteed, "a recorded pose is the pose at capture, whatever it is (even the world origin)");
        }

        [Test]
        public void TheCamera_CanLoop_AndSettingARecordingStartsOver()
        {
            var recording = SyntheticRoom.Build(0f, 90f);
            var camera = NewCamera(recording);
            camera.Loop = true;

            camera.TryCaptureFrame(75, out _);
            camera.TryCaptureFrame(75, out _);
            Assert.IsTrue(camera.TryCaptureFrame(75, out var wrapped));
            Assert.AreEqual(recording.frames[0].pose, wrapped.Pose);

            camera.Recording = recording;
            Assert.AreEqual(0, camera.NextFrameIndex);
        }

        [Test]
        public void TheCamerasProviderMembers_DescribeTheNextFrame_AndDoNotAdvance()
        {
            var recording = SyntheticRoom.Build(0f, 90f);
            var camera = NewCamera(recording);

            Assert.AreSame(recording.frames[0].jpeg, camera.GetCurrentFrameJpeg());
            Assert.AreEqual(recording.frames[0].pose, camera.GetCameraPose());
            Assert.AreEqual(recording.frames[0].intrinsics, camera.GetCameraIntrinsics());
            Assert.AreEqual(0, camera.NextFrameIndex);

            camera.Advance();

            Assert.AreEqual(recording.frames[1].pose, camera.GetCameraPose());
            Assert.AreEqual(1, camera.FramesLeft);
        }

        [Test]
        public void TheCamera_WithoutARecordingOrAnImage_HasNoFrame_AndDoesNotAdvance()
        {
            var camera = new GameObject("ReplayCamera").AddComponent<ReplayCameraSource>();
            _created.Add(camera.gameObject);
            Assert.IsFalse(camera.IsAvailable);
            Assert.IsFalse(camera.TryCaptureFrame(75, out _));
            Assert.AreEqual(Matrix4x4.zero, camera.GetCameraIntrinsics());

            var recording = SyntheticRoom.Build(0f);
            recording.frames[0].jpeg = null;
            camera.Recording = recording;
            Assert.IsFalse(camera.TryCaptureFrame(75, out _));
            Assert.AreEqual(0, camera.NextFrameIndex, "a frame without an image is not skipped past");
        }

        // ---- the scene backend ----

        [Test]
        public void TheSceneBackend_ReadsTheRecordedRoom_AsACopyStampedNow()
        {
            var recording = SyntheticRoom.Build(0f);
            var scene = NewScene(recording);

            var before = Time.timeAsDouble;
            var snapshot = scene.CaptureSnapshotAsync().Result;

            Assert.AreSame(snapshot, scene.LastSnapshot);
            Assert.IsNull(snapshot.error);
            Assert.AreEqual(6, snapshot.anchors.Count);
            Assert.AreEqual("synthetic-room", snapshot.primaryRoomUuid);
            Assert.That(snapshot.timestamp, Is.GreaterThanOrEqualTo(before));
            snapshot.anchors.Clear();
            Assert.AreEqual(6, recording.scene.anchors.Count, "playing the recording back must not change it");
        }

        [Test]
        public void TheSceneBackend_WithoutARecordedRoom_ReportsTheErrorInTheSnapshot()
        {
            var scene = NewScene(new ScanRecording());

            var snapshot = scene.CaptureSnapshotAsync().Result;

            StringAssert.Contains("No scene model", snapshot.error);
            Assert.IsEmpty(snapshot.anchors);
        }

        [Test]
        public void TheSceneBackend_MakesOnePinPerAnchor_AndHasNoMeshes()
        {
            var scene = NewScene(SyntheticRoom.Build(0f));
            scene.CaptureSnapshotAsync().Wait();

            var pins = scene.ConvertSnapshotToPins();

            Assert.AreEqual(6, pins.Count);
            CollectionAssert.AreEquivalent(new[] { "FLOOR", "CEILING", "WALL_FACE", "WALL_FACE", "WALL_FACE", "WALL_FACE" },
                pins.Select(pin => pin.label).ToArray());
            Assert.IsTrue(pins.All(pin => pin.source == PinSource.MetaScene));
            Assert.IsFalse(scene.TryBuildSceneMeshes(scene.transform, null, out var meshes));
            Assert.IsEmpty(meshes);
        }

        // ---- the recording file ----

        [Test]
        public void ARecording_RoundTripsThroughJson()
        {
            var recording = SyntheticRoom.Build(0f, 90f);

            var read = ScanRecording.FromJson(recording.ToJson());

            Assert.AreEqual(recording.name, read.name);
            Assert.AreEqual(2, read.frames.Count);
            Assert.AreEqual(recording.frames[1].id, read.frames[1].id);
            Assert.AreEqual(recording.frames[1].timestamp, read.frames[1].timestamp);
            CollectionAssert.AreEqual(recording.frames[0].jpeg, read.frames[0].jpeg);
            AssertNear(recording.frames[1].pose.position, read.frames[1].pose.position);
            Assert.That(Quaternion.Angle(recording.frames[1].pose.rotation, read.frames[1].pose.rotation), Is.LessThan(1e-3f));
            Assert.AreEqual(SyntheticRoom.FocalLengthPixels, read.frames[0].intrinsics.m00);
            Assert.AreEqual(SyntheticRoom.ImageWidth / 2f, read.frames[0].intrinsics.m02);
            Assert.AreEqual(6, read.planes.Count);
            Assert.AreEqual(recording.planes[2].label, read.planes[2].label);
            AssertNear(recording.planes[2].normal, read.planes[2].normal);
            Assert.AreEqual(6, read.scene.anchors.Count);
            Assert.AreEqual("synthetic-room", read.scene.primaryRoomUuid);
            CollectionAssert.AreEqual(recording.scene.anchors[0].labels, read.scene.anchors[0].labels);
            Assert.IsTrue(read.scene.anchors[0].hasPlane);
            AssertNear(recording.scene.anchors[0].planeSize, read.scene.anchors[0].planeSize);
        }

        [Test]
        public void AnUnknownRecordingVersion_IsRefused()
        {
            Assert.Throws<System.FormatException>(() => ScanRecording.FromJson("{\"version\": 99, \"frames\": []}"));
            Assert.Throws<System.FormatException>(() => ScanRecording.FromJson("{\"frames\": []}"));
        }

        // ---- the rig ----

        [Test]
        public void TheRig_PointsThePipelineTheRaycasterAndTheExporterAtTheRecording()
        {
            var host = new GameObject("Host");
            host.SetActive(false);
            _created.Add(host);
            var pipeline = host.AddComponent<SpatialCapturePipeline>();
            var raycaster = host.AddComponent<ViewportRaycaster>();
            var exporter = host.AddComponent<CompanionScan.Export.SpatialMapExporter>();
            var recording = SyntheticRoom.Build(0f);

            var rig = ReplayRig.Attach(host, recording, pipeline, raycaster, exporter);

            Assert.AreSame(recording, rig.Recording);
            Assert.AreSame(rig.Scene, pipeline.SceneSource);
            Assert.AreSame(rig.Scene, exporter.SceneSource);
            Assert.AreEqual(1, raycaster.SourceCount);
            Assert.AreSame(recording, rig.Camera.Recording);
        }

        private ReplayCameraSource NewCamera(ScanRecording recording)
        {
            var go = new GameObject("ReplayCamera");
            _created.Add(go);
            var camera = go.AddComponent<ReplayCameraSource>();
            camera.Recording = recording;
            return camera;
        }

        private ReplaySceneSource NewScene(ScanRecording recording)
        {
            var go = new GameObject("ReplayScene");
            _created.Add(go);
            var scene = go.AddComponent<ReplaySceneSource>();
            scene.Recording = recording;
            return scene;
        }

        private static Ray RayThrough(ReplayFrame frame, float pixelX, float pixelY)
        {
            return ViewportRaycaster.BuildRayFromIntrinsics(
                CaptureFrame.Create(frame.jpeg, frame.width, frame.height, frame.pose, frame.intrinsics), new Vector2(pixelX, pixelY));
        }

        private static void AssertLooks(Vector3 direction, ReplayFrame frame)
        {
            Assert.That(Vector3.Distance(direction, frame.pose.rotation * Vector3.forward), Is.LessThan(1e-4f),
                $"{frame.id} looks along {frame.pose.rotation * Vector3.forward}, not {direction}");
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-3f), $"expected {expected} but was {actual}");
        }
    }
}
