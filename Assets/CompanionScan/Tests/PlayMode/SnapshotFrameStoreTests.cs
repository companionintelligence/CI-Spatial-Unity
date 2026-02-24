using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    public class SnapshotFrameStoreTests
    {
        [Test]
        public void AddSnapshot_StoresFrameAndCorners()
        {
            var storeGo = new GameObject("SnapshotStore");
            var store = storeGo.AddComponent<SnapshotFrameStore>();

            var camGo = new GameObject("SnapshotCamera");
            var cam = camGo.AddComponent<Camera>();

            var jpeg = new byte[] { 1, 2, 3, 4 };
            var pose = new Pose(Vector3.one, Quaternion.identity);
            store.AddSnapshot(jpeg, 640, 480, pose, Matrix4x4.identity, 1.5f, cam);

            Assert.AreEqual(1, store.Frames.Count);
            var frame = store.Frames[0];
            Assert.AreEqual(640, frame.imageWidth);
            Assert.AreEqual(480, frame.imageHeight);
            Assert.AreEqual(4, frame.frustumCorners.Length);

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(storeGo);
        }
    }
}
