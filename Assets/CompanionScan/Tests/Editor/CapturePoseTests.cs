using System.Collections.Generic;
using System.Reflection;
using CompanionScan.Core;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// Every capture frame is stamped with the pose of the camera that took it, and pins are cast
    /// from that stamp, not from where the headset is when the server answers. The camera provider
    /// the pipeline creates at run time used to be parented to the pipeline's own root, which never
    /// moves, so its pose was the world origin for every capture.
    /// </summary>
    public class CapturePoseTests
    {
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xD9 };

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

        [Test]
        public void ProviderThatReportsTheWorldOrigin_FrameCarriesTheHeadsetPose()
        {
            var camera = NewCamera(new Vector3(1.5f, 1.7f, -2f), Quaternion.Euler(10f, 135f, 0f));
            var provider = new FakeProvider(new Pose(Vector3.zero, Quaternion.identity));

            var frame = BuildCaptureFrame(NewPipeline(provider, camera));

            Assert.That(Vector3.Distance(camera.transform.position, frame.cameraPose.position), Is.LessThan(1e-4f),
                "the frame was stamped with the world origin instead of the headset position");
            Assert.That(Quaternion.Angle(camera.transform.rotation, frame.cameraPose.rotation), Is.LessThan(1e-2f),
                "the frame was stamped with no rotation instead of the headset rotation");
        }

        [Test]
        public void ProviderThatTracksItsOwnCamera_KeepsItsPose()
        {
            // A camera that sits a few centimetres off the head, as the headset's cameras do.
            var camera = NewCamera(new Vector3(1.5f, 1.7f, -2f), Quaternion.Euler(10f, 135f, 0f));
            var ownPose = new Pose(new Vector3(1.53f, 1.69f, -1.98f), Quaternion.Euler(12f, 133f, 1f));

            var frame = BuildCaptureFrame(NewPipeline(new FakeProvider(ownPose), camera));

            Assert.That(Vector3.Distance(ownPose.position, frame.cameraPose.position), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(ownPose.rotation, frame.cameraPose.rotation), Is.LessThan(1e-2f));
        }

        [Test]
        public void HeadsetAtTheOriginLookingForward_ProviderPoseIsAccepted()
        {
            var camera = NewCamera(Vector3.zero, Quaternion.identity);
            var provider = new FakeProvider(new Pose(Vector3.zero, Quaternion.identity));

            var frame = BuildCaptureFrame(NewPipeline(provider, camera));

            Assert.That(frame.cameraPose.position.sqrMagnitude, Is.LessThan(1e-8f));
            Assert.That(Quaternion.Angle(Quaternion.identity, frame.cameraPose.rotation), Is.LessThan(1e-2f));
        }

        [Test]
        public void IsWorldOrigin_IsTrueOnlyForTheOriginWithNoRotation()
        {
            Assert.IsTrue(CapturePose.IsWorldOrigin(new Pose(Vector3.zero, Quaternion.identity)));
            Assert.IsFalse(CapturePose.IsWorldOrigin(new Pose(new Vector3(0f, 0.01f, 0f), Quaternion.identity)));
            Assert.IsFalse(CapturePose.IsWorldOrigin(new Pose(Vector3.zero, Quaternion.Euler(0f, 5f, 0f))));
        }

        [Test]
        public void Resolve_WithoutACamera_KeepsTheProviderPose()
        {
            var provider = new Pose(Vector3.zero, Quaternion.identity);

            var resolved = CapturePose.Resolve(provider, false, new Pose(Vector3.one, Quaternion.identity), out var rejected);

            Assert.IsFalse(rejected);
            Assert.AreEqual(provider, resolved);
        }

        [Test]
        public void Resolve_CameraBarelyMoved_KeepsTheProviderPose()
        {
            var provider = new Pose(Vector3.zero, Quaternion.identity);
            var nearOrigin = new Pose(new Vector3(0.02f, 0f, 0.02f), Quaternion.Euler(0.5f, 0f, 0f));

            CapturePose.Resolve(provider, true, nearOrigin, out var rejected);

            Assert.IsFalse(rejected, "a headset 3 cm from the origin is not evidence that the provider is static");
        }

        [Test]
        public void Resolve_CameraTurnedOnTheSpot_RejectsAProviderAtTheOrigin()
        {
            var provider = new Pose(Vector3.zero, Quaternion.identity);
            var turned = new Pose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

            var resolved = CapturePose.Resolve(provider, true, turned, out var rejected);

            Assert.IsTrue(rejected);
            Assert.AreEqual(turned, resolved);
        }

        private Camera NewCamera(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("CapturePoseCamera");
            _created.Add(go);
            go.transform.SetPositionAndRotation(position, rotation);
            return go.AddComponent<Camera>();
        }

        private SpatialCapturePipeline NewPipeline(ICameraProvider provider, Camera snapshotCamera)
        {
            var go = new GameObject("CapturePosePipeline");
            _created.Add(go);
            var pipeline = go.AddComponent<SpatialCapturePipeline>();
            SetField(pipeline, "_cameraProvider", provider);
            SetField(pipeline, "snapshotCamera", snapshotCamera);
            return pipeline;
        }

        private static CaptureFrame BuildCaptureFrame(SpatialCapturePipeline pipeline)
        {
            // The frame the way the pipeline gets it from a plain provider (the adapter), then built into a capture.
            var provider = (ICameraProvider)typeof(SpatialCapturePipeline)
                .GetField("_cameraProvider", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(pipeline);
            Assert.IsTrue(CameraSources.From(provider).TryCaptureFrame(75, out var cameraFrame), "the provider gave no frame");

            var method = typeof(SpatialCapturePipeline).GetMethod("BuildCaptureFrame", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "SpatialCapturePipeline.BuildCaptureFrame not found");
            return (CaptureFrame)method.Invoke(pipeline, new object[] { cameraFrame });
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }

        private sealed class FakeProvider : ICameraProvider
        {
            private readonly Pose _pose;

            public FakeProvider(Pose pose)
            {
                _pose = pose;
            }

            public bool IsAvailable => true;
            public void StartCapture() { }
            public void StopCapture() { }
            public byte[] GetCurrentFrameJpeg(int quality = 75) => Jpeg;
            public Matrix4x4 GetCameraIntrinsics() => Matrix4x4.identity;
            public Pose GetCameraPose() => _pose;
        }
    }
}
