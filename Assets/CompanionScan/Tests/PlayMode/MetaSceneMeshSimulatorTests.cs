using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CompanionScan.Tests.PlayMode
{
    public class MetaSceneMeshSimulatorTests
    {
        [UnityTest]
        public IEnumerator MetaSceneSimulator_CapturesSceneMesh()
        {
            if (!IsMetaSimulatorAvailable())
            {
                Assert.Ignore("Meta XR simulator not available in this session.");
            }

            var root = new GameObject("MetaSceneMeshTestRoot");
            var tracking = new GameObject("TrackingSpace").transform;
            tracking.SetParent(root.transform, false);

            var collector = root.AddComponent<MetaSceneCollector>();
            SetPrivateField(collector, "trackingSpace", tracking);
            SetPrivateField(collector, "includeMeshes", true);

            var task = collector.CaptureSnapshotAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            var snapshot = task.Result;
            if (!string.IsNullOrEmpty(snapshot.error))
            {
                Assert.Inconclusive("Meta scene error: " + snapshot.error);
            }

            bool hasMesh = snapshot.anchors.Any(a => a.hasMesh && a.meshVertexCount > 0);
            if (!hasMesh)
            {
                Assert.Inconclusive("No mesh anchors reported. Ensure Scene Model data is present in the simulator.");
            }

            Assert.IsTrue(collector.TryBuildSceneMeshes(root.transform, null, out var meshes),
                "Expected to build scene meshes from Meta scene data.");
            Assert.IsNotNull(meshes, "Expected non-null mesh list.");
            Assert.IsTrue(meshes.Count > 0, "Expected at least one scene mesh GameObject.");
        }

        private static bool IsMetaSimulatorAvailable()
        {
            var ovrManagerType = FindType("OVRManager");
            var ovrSceneManagerType = FindType("OVRSceneManager");
            if (ovrManagerType == null || ovrSceneManagerType == null)
            {
                return false;
            }

            var manager = UnityEngine.Object.FindObjectOfType(ovrManagerType);
            var sceneManager = UnityEngine.Object.FindObjectOfType(ovrSceneManagerType);
            return manager != null && sceneManager != null;
        }

        private static Type FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
