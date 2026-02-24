using System.Linq;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    public class SnapshotFrustumVisualizerTests
    {
        [Test]
        public void FreezeFrustum_CreatesLineRenderers()
        {
            var root = new GameObject("FrustumRoot");
            var cameraGo = new GameObject("FrustumCamera");
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.tag = "MainCamera";

            var vis = root.AddComponent<SnapshotFrustumVisualizer>();
            vis.FreezeFrustum(2.0f);

            var lineCount = root.GetComponentsInChildren<LineRenderer>().Length;
            Assert.GreaterOrEqual(lineCount, 5, "Expected rect + 4 rays.");

            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(root);
        }
    }
}
