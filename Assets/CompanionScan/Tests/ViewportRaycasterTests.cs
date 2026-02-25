using NUnit.Framework;
using UnityEngine;
using CompanionScan.Mapping;

namespace CompanionScan.Tests
{
    public class ViewportRaycasterTests
    {
        [Test]
        public void BuildRayFromIntrinsics_UsesIntrinsicsAndPose()
        {
            var frame = new CaptureFrame
            {
                imageWidth = 100,
                imageHeight = 100,
                cameraPose = new Pose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f)),
                cameraIntrinsics = new Matrix4x4
                {
                    m00 = 100f,
                    m11 = 100f,
                    m02 = 50f,
                    m12 = 50f
                }
            };

            var ray = ViewportRaycaster.BuildRayFromIntrinsics(frame, new Vector2(50f, 50f));
            var expected = frame.cameraPose.rotation * Vector3.forward;

            Assert.That(Vector3.Distance(ray.direction, expected.normalized), Is.LessThan(0.0001f));
        }

        [Test]
        public void BuildRayFromIntrinsics_OffsetsPixelPosition()
        {
            var frame = new CaptureFrame
            {
                imageWidth = 100,
                imageHeight = 100,
                cameraPose = new Pose(Vector3.zero, Quaternion.identity),
                cameraIntrinsics = new Matrix4x4
                {
                    m00 = 100f,
                    m11 = 100f,
                    m02 = 50f,
                    m12 = 50f
                }
            };

            var ray = ViewportRaycaster.BuildRayFromIntrinsics(frame, new Vector2(60f, 50f));
            var expected = new Vector3(0.1f, 0f, 1f).normalized;

            Assert.That(Vector3.Distance(ray.direction, expected), Is.LessThan(0.0001f));
        }
    }
}
