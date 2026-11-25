using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// Characterization tests: they record what <see cref="CaptureFrame"/> does today. A test with
    /// "Currently" in its name records a result that looks doubtful, not a requirement.
    /// </summary>
    public class CaptureFrameCharacterizationTests
    {
        // ---- HasIntrinsics: both focal lengths (m00 and m11) must be greater than zero ----

        [TestCase(500f, 500f)]
        [TestCase(300f, 700f)]
        [TestCase(1f, 1f)]
        [TestCase(0.001f, 0.001f)]
        public void HasIntrinsics_IsTrueWhenBothFocalLengthsAreGreaterThanZero(float fx, float fy)
        {
            Assert.IsTrue(FrameWithFocalLengths(fx, fy).HasIntrinsics);
        }

        [TestCase(0f, 500f)]
        [TestCase(500f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(-500f, 500f)]
        [TestCase(500f, -500f)]
        [TestCase(-1f, -1f)]
        [TestCase(float.NaN, 500f)]
        [TestCase(500f, float.NaN)]
        public void HasIntrinsics_IsFalseWhenEitherFocalLengthIsZeroNegativeOrNotANumber(float fx, float fy)
        {
            Assert.IsFalse(FrameWithFocalLengths(fx, fy).HasIntrinsics);
        }

        // The rule is only "greater than zero": an infinite focal length passes.
        [TestCase(float.PositiveInfinity, 500f)]
        [TestCase(500f, float.PositiveInfinity)]
        public void HasIntrinsics_Currently_IsTrueForAnInfiniteFocalLength(float fx, float fy)
        {
            Assert.IsTrue(FrameWithFocalLengths(fx, fy).HasIntrinsics);
        }

        [Test]
        public void HasIntrinsics_IsFalseForAFrameThatWasNeverGivenIntrinsics()
        {
            Assert.IsFalse(new CaptureFrame().HasIntrinsics);
        }

        [Test]
        public void HasIntrinsics_IsFalseForTheZeroMatrix_ThatTheQuestAndMobileProvidersReturnWithoutIntrinsics()
        {
            Assert.IsFalse(new CaptureFrame { cameraIntrinsics = Matrix4x4.zero }.HasIntrinsics);
        }

        // The identity matrix (no camera provider; DesktopCameraProvider) counts as intrinsics: a focal length of one pixel.
        [Test]
        public void HasIntrinsics_Currently_IsTrueForTheIdentityMatrix_ThatStandsInWhenThereAreNoIntrinsics()
        {
            var frame = CaptureFrame.Create(new byte[] { 1 }, 640, 480, Pose.identity, Matrix4x4.identity);

            Assert.IsTrue(frame.HasIntrinsics);
        }

        [Test]
        public void HasIntrinsics_LooksOnlyAtTheTwoFocalLengths()
        {
            var focalLengthsOnly = Matrix4x4.zero;
            focalLengthsOnly.m00 = 500f;
            focalLengthsOnly.m11 = 500f;
            var everythingButTheFocalLengths = Matrix4x4.identity;
            everythingButTheFocalLengths.m00 = 0f;
            everythingButTheFocalLengths.m11 = 0f;
            everythingButTheFocalLengths.m02 = 320f;
            everythingButTheFocalLengths.m12 = 240f;

            Assert.IsTrue(new CaptureFrame { cameraIntrinsics = focalLengthsOnly, imageWidth = 0, imageHeight = 0 }.HasIntrinsics,
                "the principal point and the image size are not checked");
            Assert.IsFalse(new CaptureFrame { cameraIntrinsics = everythingButTheFocalLengths, imageWidth = 640, imageHeight = 480 }.HasIntrinsics);
        }

        // ---- Create ----

        [Test]
        public void Create_StoresItsArgumentsAsTheyAreAndKeepsTheSameByteArray()
        {
            var bytes = new byte[] { 1, 2, 3 };
            var pose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 30f));
            var intrinsics = Matrix4x4.zero;
            intrinsics.m00 = 500f;
            intrinsics.m11 = 510f;
            intrinsics.m02 = 320f;
            intrinsics.m12 = 240f;

            var frame = CaptureFrame.Create(bytes, 640, 480, pose, intrinsics);

            Assert.AreSame(bytes, frame.jpegBytes, "the bytes are not copied");
            Assert.AreEqual(640, frame.imageWidth);
            Assert.AreEqual(480, frame.imageHeight);
            Assert.AreEqual(pose, frame.cameraPose);
            Assert.AreEqual(intrinsics, frame.cameraIntrinsics);
            Assert.IsTrue(frame.HasIntrinsics);
        }

        [Test]
        public void Create_LeavesTheIdTheSessionAndTheFrameIndexForTheCallerToSet()
        {
            var frame = CaptureFrame.Create(new byte[] { 1 }, 4, 4, Pose.identity, Matrix4x4.zero);

            Assert.IsNull(frame.id);
            Assert.IsNull(frame.sessionId);
            Assert.AreEqual(0, frame.frameIndex);
        }

        [Test]
        public void Create_StampsTheTimeItWasCalledAt()
        {
            var before = Time.timeAsDouble;

            var frame = CaptureFrame.Create(new byte[] { 1 }, 4, 4, Pose.identity, Matrix4x4.zero);

            var after = Time.timeAsDouble;
            Assert.That(frame.timestamp, Is.InRange(before, after));
        }

        // Nothing is checked: a frame can have no bytes and a negative or zero size.
        [Test]
        public void Create_Currently_AcceptsNoBytesAndNonPositiveSizes()
        {
            var frame = CaptureFrame.Create(null, -5, 0, default(Pose), Matrix4x4.zero);

            Assert.IsNotNull(frame);
            Assert.IsNull(frame.jpegBytes);
            Assert.AreEqual(-5, frame.imageWidth);
            Assert.AreEqual(0, frame.imageHeight);
            Assert.IsFalse(frame.HasIntrinsics);
        }

        [Test]
        public void Create_ReturnsANewFrameEachTime()
        {
            var bytes = new byte[] { 1 };

            var first = CaptureFrame.Create(bytes, 4, 4, Pose.identity, Matrix4x4.zero);
            var second = CaptureFrame.Create(bytes, 4, 4, Pose.identity, Matrix4x4.zero);

            Assert.AreNotSame(first, second);
        }

        private static CaptureFrame FrameWithFocalLengths(float fx, float fy)
        {
            var intrinsics = Matrix4x4.zero;
            intrinsics.m00 = fx;
            intrinsics.m11 = fy;
            return new CaptureFrame { cameraIntrinsics = intrinsics };
        }
    }
}
