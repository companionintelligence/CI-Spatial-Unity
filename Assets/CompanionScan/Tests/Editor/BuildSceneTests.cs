using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CompanionScan.Export;
using CompanionScan.Mapping;
using CompanionScan.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// The scene the APK is built from: every reference in it resolves, and the parts that put pins
    /// and labels in the room and export them are connected. Commit a862495 deleted the label
    /// prefab the scene's pins and labels pointed at, and the app then showed no 3D labels or pins
    /// at all without anything failing.
    /// </summary>
    public class BuildSceneTests
    {
        private const string CaptureScenePath = "Assets/CompanionScan/Scenes/CompanionCapture.unity";

        // The GUID of the label prefab deleted in a862495.
        private const string DeletedLabelPrefabGuid = "6b375e879b1134b4d8f73e698cb23513";

        // Unity's built-in resources have no .meta file.
        private static readonly HashSet<string> BuiltInGuids = new HashSet<string>
        {
            "0000000000000000e000000000000000",
            "0000000000000000f000000000000000",
            "0000000000000000d000000000000000"
        };

        private static readonly Regex GuidReference = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.Compiled);

        private Scene _scene;
        private bool _openedHere;

        [OneTimeSetUp]
        public void OpenCaptureScene()
        {
            _scene = SceneManager.GetSceneByPath(CaptureScenePath);
            if (!_scene.isLoaded)
            {
                _scene = EditorSceneManager.OpenScene(CaptureScenePath, OpenSceneMode.Additive);
                _openedHere = true;
            }
        }

        [OneTimeTearDown]
        public void CloseCaptureScene()
        {
            if (_openedHere && _scene.IsValid() && _scene.isLoaded)
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        // References

        [Test]
        public void TheCaptureSceneIsTheOnlyBuildScene()
        {
            CollectionAssert.AreEqual(new[] { CaptureScenePath }, BuildScenePaths().ToList());
        }

        [Test]
        public void EveryAssetTheBuildScenesReferenceExists()
        {
            var problems = new List<string>();
            var visited = new HashSet<string>();
            foreach (var path in BuildScenePaths())
            {
                CollectUnresolvedReferences(path, visited, problems);
            }

            CollectionAssert.IsEmpty(problems,
                "References to assets that do not exist (a deleted prefab, script, material or texture):\n  " +
                string.Join("\n  ", problems));
        }

        [Test]
        public void TheReferenceCheck_FindsTheDeletedLabelPrefab()
        {
            // How the scene pointed at the label prefab after a862495 deleted it.
            const string sceneLine =
                "labelPrefab: {fileID: 7011814298867290955, guid: " + DeletedLabelPrefabGuid + ", type: 3}";

            CollectionAssert.AreEqual(new[] { DeletedLabelPrefabGuid }, UnresolvedGuids(sceneLine).ToList());
            CollectionAssert.IsEmpty(UnresolvedGuids(File.ReadAllText(CaptureScenePath)).Where(g => g == DeletedLabelPrefabGuid));
        }

        [Test]
        public void NoObjectInTheCaptureSceneHasAMissingScript()
        {
            var missing = AllGameObjects()
                .Where(go => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
                .Select(HierarchyPath)
                .ToList();

            CollectionAssert.IsEmpty(missing, "Objects with a missing script:\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void NoComponentInTheCaptureSceneHasAMissingReference()
        {
            var missing = new List<string>();
            foreach (var go in AllGameObjects())
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        continue; // a missing script, reported by its own test
                    }

                    var property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue == null &&
                            property.objectReferenceEntityIdValue.IsValid())
                        {
                            missing.Add($"{HierarchyPath(go)} {component.GetType().Name}.{property.propertyPath}");
                        }
                    }
                }
            }

            CollectionAssert.IsEmpty(missing, "Missing references:\n  " + string.Join("\n  ", missing));
        }

        // Pins and labels

        [Test]
        public void PinsAndLabels_HaveTheirPrefabsAndRoots()
        {
            var pins = Single<SpatialPinManager>();
            var labels = Single<LabelRenderer>();

            var pinPrefab = Reference<GameObject>(pins, "pinPrefab");
            var labelPrefab = Reference<GameObject>(labels, "labelPrefab");
            Assert.IsNotNull(pinPrefab, "SpatialPinManager has no pin prefab, so it shows no pins");
            Assert.IsNotNull(labelPrefab, "LabelRenderer has no label prefab, so it shows no labels");
            Assert.IsNotNull(labelPrefab.GetComponent<SpatialLabelView>(), "the label prefab cannot show text");
            Assert.IsNotNull(Reference<Object>(labelPrefab.GetComponent<SpatialLabelView>(), "text"),
                "the label prefab's view has no TextMesh Pro text");

            Assert.IsNotNull(Reference<Transform>(pins, "pinRoot"), "SpatialPinManager has no pin root");
            Assert.IsNotNull(Reference<Transform>(labels, "labelRoot"), "LabelRenderer has no label root");
            Assert.AreSame(pins, Reference<SpatialPinManager>(labels, "pinManager"),
                "LabelRenderer does not label the scene's pins");
            Assert.AreSame(labels, Reference<LabelRenderer>(Single<SpatialCapturePipeline>(), "labelRenderer"));
            Assert.AreSame(pins, Reference<SpatialPinManager>(Single<SpatialCapturePipeline>(), "pinManager"));
        }

        [Test]
        public void PinAndLabelObjects_StayOutOfTheExportAndOfTheRaycasts()
        {
            var exportRoot = Reference<Transform>(Single<SpatialMapExporter>(), "exportRoot")
                             ?? Single<SpatialMapExporter>().transform;
            var pinRoot = Reference<Transform>(Single<SpatialPinManager>(), "pinRoot");
            var labelRoot = Reference<Transform>(Single<LabelRenderer>(), "labelRoot");
            Assert.IsNotNull(pinRoot, "SpatialPinManager has no pin root");
            Assert.IsNotNull(labelRoot, "LabelRenderer has no label root");

            // Under the export root they would be written into every glTF as geometry.
            Assert.IsFalse(pinRoot.IsChildOf(exportRoot), "pins are created inside the export root");
            Assert.IsFalse(labelRoot.IsChildOf(exportRoot), "labels are created inside the export root");

            // A collider would let the next capture's raycast land on an earlier pin or label.
            foreach (var prefab in new[]
                     {
                         Reference<GameObject>(Single<SpatialPinManager>(), "pinPrefab"),
                         Reference<GameObject>(Single<LabelRenderer>(), "labelPrefab")
                     })
            {
                CollectionAssert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), prefab.name + " has colliders");
            }
        }

        [Test]
        public void Pins_StayUntilClearedOrReplaced()
        {
            var pins = new SerializedObject(Single<SpatialPinManager>());
            Assert.IsFalse(pins.FindProperty("enablePinLifecycle").boolValue,
                "age-based pruning removes every earlier capture's pins; labels persist across captures");
        }

        // Helpers

        private static IEnumerable<string> BuildScenePaths()
        {
            return EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path);
        }

        private static IEnumerable<string> UnresolvedGuids(string text)
        {
            return GuidReference.Matches(text)
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .Where(guid => !BuiltInGuids.Contains(guid) && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)));
        }

        /// <summary>
        /// Every GUID in <paramref name="assetPath"/> must name an asset; the project's own prefabs
        /// and materials it references are checked the same way.
        /// </summary>
        private static void CollectUnresolvedReferences(string assetPath, HashSet<string> visited, List<string> problems)
        {
            if (!visited.Add(assetPath))
            {
                return;
            }

            var lines = File.ReadAllLines(assetPath);
            foreach (var line in lines)
            {
                foreach (Match match in GuidReference.Matches(line))
                {
                    var guid = match.Groups[1].Value;
                    if (BuiltInGuids.Contains(guid))
                    {
                        continue;
                    }

                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || AssetDatabase.GetMainAssetTypeAtPath(path) == null)
                    {
                        problems.Add($"{assetPath}: {line.Trim()}");
                        continue;
                    }

                    if (path.StartsWith("Assets/") && (path.EndsWith(".prefab") || path.EndsWith(".mat")))
                    {
                        CollectUnresolvedReferences(path, visited, problems);
                    }
                }
            }
        }

        private IEnumerable<GameObject> AllGameObjects()
        {
            Assert.IsTrue(_scene.isLoaded, "the capture scene did not open");
            return _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject);
        }

        private List<T> Components<T>() where T : Component
        {
            return _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToList();
        }

        private T Single<T>() where T : Component
        {
            var found = Components<T>();
            Assert.AreEqual(1, found.Count, $"expected one {typeof(T).Name} in the capture scene");
            return found[0];
        }

        private static T Reference<T>(Object owner, string field) where T : Object
        {
            var property = new SerializedObject(owner).FindProperty(field);
            Assert.IsNotNull(property, $"{owner.GetType().Name} has no serialized field '{field}'");
            return property.objectReferenceValue as T;
        }

        private static string HierarchyPath(GameObject go)
        {
            var path = go.name;
            for (var parent = go.transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }
    }
}
