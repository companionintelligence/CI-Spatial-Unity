using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CompanionScan.Export;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CompanionScan.Tests.PlayMode
{
    /// <summary>
    /// What Y (save and export) writes on the headset: a glTF with the pins, the floor plan and the
    /// session's snapshots with their camera poses (README, controller table). The exporter looked
    /// up UnityGLTF by reflection with signatures UnityGLTF 2.x does not have, so it wrote no glTF
    /// at all, and the scene left its pin manager, boundary and Space Setup reader unset.
    /// </summary>
    public class SpatialMapExporterTests
    {
        private string _token;
        private string _outputDir;
        private GameObject _root;
        private Material _floorMaterial;

        [SetUp]
        public void SetUp()
        {
            _token = "test-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            // With no Space Setup data the export goes to the "default" space.
            _outputDir = Path.Combine(Application.persistentDataPath, "exports", "default");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            if (_floorMaterial != null)
            {
                Object.DestroyImmediate(_floorMaterial);
            }

            if (Directory.Exists(_outputDir))
            {
                foreach (var file in Directory.GetFiles(_outputDir, "*" + _token + "*", SearchOption.AllDirectories))
                {
                    File.Delete(file);
                }
            }

            // Remove the folders the export made, when nothing else is in them.
            foreach (var dir in new[]
                     {
                         Path.Combine(_outputDir, "snapshots"),
                         _outputDir,
                         Path.Combine(Application.persistentDataPath, "exports")
                     })
            {
                if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
                {
                    Directory.Delete(dir);
                }
            }
        }

        [UnityTest]
        public IEnumerator Export_WritesAGltfWithThePinsFloorPlanAndSnapshots()
        {
            _root = new GameObject("CompanionScan");
            _root.SetActive(false);
            var pins = _root.AddComponent<SpatialPinManager>();
            var boundary = _root.AddComponent<BoundaryProvider>();
            var snapshots = _root.AddComponent<SnapshotFrameStore>();
            var exporter = _root.AddComponent<SpatialMapExporter>();
            _floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

            SetPrivateField(exporter, "pinManager", pins);
            SetPrivateField(exporter, "boundaryProvider", boundary);
            SetPrivateField(exporter, "snapshotStore", snapshots);
            SetPrivateField(exporter, "floorMaterial", _floorMaterial);
            SetPrivateField(exporter, "fileName", "spatial-map-" + _token);
            SetPrivateField(snapshots, "idPrefix", _token);
            _root.SetActive(true);

            pins.AddPin(new SpatialPin
            {
                label = "chair", semanticLabel = "CHAIR", source = PinSource.CvServer,
                position = new Vector3(-1f, 0f, 2f), normal = Vector3.up, trackId = 1
            });
            pins.AddPin(new SpatialPin
            {
                label = "lamp", semanticLabel = "LAMP", source = PinSource.CvServer,
                position = new Vector3(1f, 0f, 2.5f), normal = Vector3.up, trackId = 2
            });
            pins.AddPin(new SpatialPin
            {
                label = "TABLE", semanticLabel = "TABLE", source = PinSource.MetaScene, anchorUuid = "anchor-1",
                position = new Vector3(0f, 0.7f, 0.5f), normal = Vector3.up, score = 1f
            });

            var image = new Texture2D(8, 8, TextureFormat.RGB24, false);
            var jpeg = image.EncodeToJPG();
            Object.DestroyImmediate(image);
            var cameraPose = new Pose(new Vector3(0.2f, 1.6f, -0.3f), Quaternion.Euler(10f, 30f, 0f));
            snapshots.AddSnapshot(CaptureFrame.Create(jpeg, 8, 8, cameraPose, Matrix4x4.identity), 1f, null);

            var export = (Task)typeof(SpatialMapExporter)
                .GetMethod("ExportInternalAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(exporter, new object[] { false, true });
            while (!export.IsCompleted)
            {
                yield return null;
            }

            Assert.IsFalse(export.IsFaulted, export.Exception?.ToString());
            var result = export.GetType().GetProperty("Result").GetValue(export);
            var gltfPath = (string)result.GetType().GetField("gltfPath").GetValue(result);
            var binPath = (string)result.GetType().GetField("binPath").GetValue(result);

            Assert.IsTrue(File.Exists(gltfPath), "no glTF was written");
            Assert.IsTrue(File.Exists(binPath), "no glTF buffer was written");
            StringAssert.StartsWith(Path.GetFullPath(Application.persistentDataPath), Path.GetFullPath(gltfPath),
                "the export left the app's storage");

            // JsonUtility reads what this test needs (test assemblies do not reference Newtonsoft).
            var json = File.ReadAllText(gltfPath);
            var gltf = JsonUtility.FromJson<GltfJson>(json);
            Assert.IsNotNull(gltf.extras, "the glTF has no extras");
            var extras = gltf.extras;

            CollectionAssert.AreEquivalent(new[] { "chair", "lamp", "TABLE" }, extras.pins.Select(pin => pin.label).ToList());
            CollectionAssert.AreEquivalent(new[] { "CvServer", "CvServer", "MetaScene" },
                extras.pins.Select(pin => pin.source).ToList());
            Assert.GreaterOrEqual(CountPoints(json, "floorplan"), 3, "the floor plan is empty");

            var snapshot = extras.snapshots.Single();
            var position = snapshot.camera.position;
            Assert.That(Vector3.Distance(cameraPose.position, new Vector3(position[0], position[1], position[2])),
                Is.LessThan(1e-4f), "the snapshot's camera pose is wrong");
            Assert.IsTrue(File.Exists(Path.Combine(_outputDir, snapshot.image)), "the snapshot image is missing");

            Assert.IsFalse(extras.meta_scene.available, "no Space Setup reader was given");

            // The floor plan is in the glTF's geometry too, not only in its extras.
            CollectionAssert.Contains(gltf.nodes.Select(node => node.name).ToList(), "FloorplanMesh");
            Assert.IsNotEmpty(gltf.meshes, "the glTF has no meshes");
        }

        /// <summary>Number of [x, y, z] points in the first array named <paramref name="key"/>.</summary>
        private static int CountPoints(string json, string key)
        {
            int keyIndex = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return -1;
            }

            int depth = 0, count = 0;
            for (int i = json.IndexOf('[', keyIndex); i >= 0 && i < json.Length; i++)
            {
                if (json[i] == '[')
                {
                    depth++;
                    if (depth == 2)
                    {
                        count++;
                    }
                }
                else if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
            }

            return count;
        }

#pragma warning disable 0649 // filled by JsonUtility
        [Serializable] private class GltfJson { public NodeJson[] nodes; public NodeJson[] meshes; public ExtrasJson extras; }
        [Serializable] private class NodeJson { public string name; }
        [Serializable] private class ExtrasJson { public PinJson[] pins; public SnapshotJson[] snapshots; public MetaSceneJson meta_scene; }
        [Serializable] private class PinJson { public string label; public string source; }
        [Serializable] private class SnapshotJson { public string image; public CameraJson camera; }
        [Serializable] private class CameraJson { public float[] position; }
        [Serializable] private class MetaSceneJson { public bool available; }
#pragma warning restore 0649

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} not found");
            field.SetValue(target, value);
        }
    }
}
