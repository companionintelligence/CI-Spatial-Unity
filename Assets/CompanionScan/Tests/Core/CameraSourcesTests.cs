using CompanionScan.Core;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Core.Tests
{
    public class CameraSourcesTests
    {
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xD9 };

        [Test]
        public void From_Null_IsNull()
        {
            Assert.IsNull(CameraSources.From(null));
        }

        [Test]
        public void From_ACameraSource_IsTheSameObject()
        {
            var source = new FakeSource();

            Assert.AreSame(source, CameraSources.From(source));
        }

        [Test]
        public void From_APlainProvider_IsAnAdapter_ThatTakesFrameIntrinsicsAndPoseFromIt()
        {
            var pose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
            var intrinsics = Intrinsics(500f, 500f, 320f, 240f);
            var provider = new FakeProvider(Jpeg, pose, intrinsics);

            var before = Time.timeAsDouble;
            var source = CameraSources.From(provider);
            var taken = source.TryCaptureFrame(80, out var frame);
            var after = Time.timeAsDouble;

            Assert.IsTrue(taken);
            Assert.AreSame(Jpeg, frame.Jpeg);
            Assert.AreEqual(80, provider.LastQuality);
            Assert.AreEqual(pose, frame.Pose);
            Assert.AreEqual(intrinsics, frame.Intrinsics);
            Assert.That(frame.Timestamp, Is.InRange(before, after));
            Assert.AreEqual(0, frame.Width, "a plain provider does not say the image size");
            Assert.AreEqual(0, frame.Height);
        }

        // A plain provider's pose is whatever its transform says now, which the pipeline checks against the headset.
        [Test]
        public void TheAdaptersPose_IsNotGuaranteed_ButANativeSourcesIs()
        {
            CameraSources.From(new FakeProvider(Jpeg, Pose.identity, Matrix4x4.zero)).TryCaptureFrame(75, out var adapted);
            new FakeSource().TryCaptureFrame(75, out var native);

            Assert.IsFalse(adapted.PoseIsGuaranteed);
            Assert.IsTrue(native.PoseIsGuaranteed);
            Assert.IsTrue(new CameraFrame(Jpeg, 1, 1, 0.0, Pose.identity, Matrix4x4.zero).PoseIsGuaranteed, "guaranteed unless said otherwise");
        }

        [Test]
        public void TheAdapter_HasNoFrame_WhenTheProviderGivesNoneOrAnEmptyOne()
        {
            Assert.IsFalse(CameraSources.From(new FakeProvider(null, Pose.identity, Matrix4x4.zero)).TryCaptureFrame(75, out _));
            Assert.IsFalse(CameraSources.From(new FakeProvider(new byte[0], Pose.identity, Matrix4x4.zero)).TryCaptureFrame(75, out _));
        }

        [Test]
        public void TheAdapter_ReadsTheProviderOncePerCapture()
        {
            var provider = new FakeProvider(Jpeg, Pose.identity, Matrix4x4.zero);
            var source = CameraSources.From(provider);

            source.TryCaptureFrame(75, out _);
            source.TryCaptureFrame(75, out _);

            Assert.AreEqual(2, provider.FramesTaken);
        }

        [Test]
        public void TheAdapter_PassesTheCameraLifecycleThrough()
        {
            var provider = new FakeProvider(Jpeg, Pose.identity, Matrix4x4.zero);
            var source = CameraSources.From(provider);

            source.StartCapture();
            source.StopCapture();

            Assert.AreEqual(1, provider.Starts);
            Assert.AreEqual(1, provider.Stops);
            Assert.IsTrue(source.IsAvailable);
            Assert.AreEqual(Matrix4x4.zero, source.GetCameraIntrinsics());
            Assert.AreEqual(Pose.identity, source.GetCameraPose());
        }

        private static Matrix4x4 Intrinsics(float fx, float fy, float cx, float cy)
        {
            var matrix = Matrix4x4.zero;
            matrix.m00 = fx;
            matrix.m11 = fy;
            matrix.m02 = cx;
            matrix.m12 = cy;
            return matrix;
        }

        private class FakeProvider : ICameraProvider
        {
            private readonly byte[] _jpeg;
            private readonly Pose _pose;
            private readonly Matrix4x4 _intrinsics;

            public FakeProvider(byte[] jpeg, Pose pose, Matrix4x4 intrinsics)
            {
                _jpeg = jpeg;
                _pose = pose;
                _intrinsics = intrinsics;
            }

            public int FramesTaken { get; private set; }
            public int LastQuality { get; private set; }
            public int Starts { get; private set; }
            public int Stops { get; private set; }
            public bool IsAvailable => true;
            public void StartCapture() => Starts++;
            public void StopCapture() => Stops++;

            public byte[] GetCurrentFrameJpeg(int quality = 75)
            {
                FramesTaken++;
                LastQuality = quality;
                return _jpeg;
            }

            public Matrix4x4 GetCameraIntrinsics() => _intrinsics;
            public Pose GetCameraPose() => _pose;
        }

        private sealed class FakeSource : FakeProvider, ICameraSource
        {
            public FakeSource() : base(Jpeg, Pose.identity, Matrix4x4.zero)
            {
            }

            public bool TryCaptureFrame(int jpegQuality, out CameraFrame frame)
            {
                frame = new CameraFrame(Jpeg, 4, 4, 1.0, Pose.identity, Matrix4x4.zero);
                return true;
            }
        }
    }
}
