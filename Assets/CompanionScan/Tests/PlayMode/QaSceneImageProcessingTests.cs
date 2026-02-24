using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CompanionScan.Core;
using CompanionScan.Mapping;
using CompanionScan.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace CompanionScan.Tests.PlayMode
{
    public class QaSceneImageProcessingTests
    {
        private const string ServerUrlEnv = "CompanionScan_TEST_SERVER_URL";
        private const string QaScenePath = "Assets/CompanionScan/Scenes/CompanionScanQA.unity";

        private static readonly string ExternalFixturesPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "test"));

        [UnityTest]
        public IEnumerator QaScene_ProcessAllImages_Render2DAnd3D()
        {
            var config = ScriptableObject.CreateInstance<ServerConfig>();
            var envUrl = Environment.GetEnvironmentVariable(ServerUrlEnv);
            config.baseUrl = string.IsNullOrWhiteSpace(envUrl) ? config.ResolveBaseUrl() : envUrl;
            if (string.IsNullOrWhiteSpace(config.baseUrl))
            {
                Assert.Ignore($"Set {ServerUrlEnv} or ServerConfig.baseUrl to run QA scene tests.");
            }

            if (!File.Exists(QaScenePath))
            {
                Assert.Inconclusive("QA scene missing: " + QaScenePath);
            }

            var images = EnumerateExternalImages();
            if (images.Count == 0)
            {
                Assert.Inconclusive("No jpg/png fixtures found at " + ExternalFixturesPath);
            }

        #if UNITY_EDITOR
            var loadOp = EditorSceneManager.LoadSceneInPlayMode(QaScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!loadOp.isDone)
            {
                yield return null;
            }
        #else
            var loadOp = SceneManager.LoadSceneAsync(Path.GetFileNameWithoutExtension(QaScenePath), LoadSceneMode.Single);
            while (!loadOp.isDone)
            {
                yield return null;
            }
        #endif

            var client = new CVServerClient(config);

            var systemsRoot = new GameObject("QaTestSystems");
            var bboxRenderer = systemsRoot.AddComponent<BBoxRenderer>();
            var labelRenderer = systemsRoot.AddComponent<LabelRenderer>();
            var raycaster = systemsRoot.AddComponent<ViewportRaycaster>();

            var labelRoot = new GameObject("QaLabelRoot").transform;
            labelRoot.SetParent(systemsRoot.transform, false);
            var labelPrefab = new GameObject("QaLabelPrefab");
            labelPrefab.SetActive(false);

            SetPrivateField(labelRenderer, "labelRoot", labelRoot);
            SetPrivateField(labelRenderer, "labelPrefab", labelPrefab);
            SetPrivateField(labelRenderer, "persistLabels", false);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.position = new Vector3(0f, 0f, 2f);
            floor.transform.localScale = Vector3.one * 4f;

            foreach (var path in images)
            {
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[QA-Scene] Read failed: " + Path.GetFileName(path) + " - " + ex.Message);
                    continue;
                }

                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!tex.LoadImage(bytes))
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    continue;
                }

                var florenceTask = client.FlorenceAsync(bytes, "<MORE_DETAILED_CAPTION>");
                while (!florenceTask.IsCompleted) yield return null;
                if (florenceTask.IsFaulted)
                {
                    Assert.Fail(florenceTask.Exception?.GetBaseException().Message);
                }
                Assert.IsNotNull(florenceTask.Result);

                var detectTask = client.DetectAsync(bytes, "object . furniture .");
                while (!detectTask.IsCompleted) yield return null;
                if (detectTask.IsFaulted)
                {
                    Assert.Fail(detectTask.Exception?.GetBaseException().Message);
                }
                var detection = detectTask.Result;
                Assert.IsNotNull(detection);

                var segmentTask = client.SegmentAsync(bytes, "object . furniture .");
                while (!segmentTask.IsCompleted) yield return null;
                if (segmentTask.IsFaulted)
                {
                    Assert.Fail(segmentTask.Exception?.GetBaseException().Message);
                }
                Assert.IsNotNull(segmentTask.Result);

                bboxRenderer.RenderBoxes(detection, tex);

                var expected = CountNormalizableBoxes(detection, tex.width, tex.height);
                var containerGo = GameObject.Find("BBOX Container") ?? GameObject.Find("BBoxContainer");
                Assert.IsNotNull(containerGo, "Expected BBOX container to be created.");
                Assert.AreEqual(expected, containerGo.transform.childCount,
                    "Expected 2D bbox overlay count to match detections.");

                var positions = new List<Vector3>();
                var labels = new List<string>();
                int max = Mathf.Min(detection.bboxes.Count, detection.labels.Count);
                for (int i = 0; i < max; i++)
                {
                    var bbox = detection.bboxes[i];
                    if (!BoundingBoxUtils.TryNormalize(bbox, tex.width, tex.height, out Rect norm))
                    {
                        continue;
                    }

                    float u = norm.x + norm.width * 0.5f;
                    float v = 1f - (norm.y + norm.height * 0.5f);
                    if (!raycaster.TryRaycastViewport(new Vector2(u, v), out Vector3 hit, out _))
                    {
                        hit = Vector3.forward * 2f;
                    }

                    positions.Add(hit);
                    labels.Add(detection.labels[i]);
                }

                labelRenderer.RenderLabels(positions, labels);
                Assert.AreEqual(positions.Count, labelRoot.childCount,
                    "Expected 3D label count to match detections.");

                labelRenderer.Clear();
                UnityEngine.Object.DestroyImmediate(tex);
            }

            UnityEngine.Object.DestroyImmediate(floor);
            UnityEngine.Object.DestroyImmediate(labelPrefab);
            UnityEngine.Object.DestroyImmediate(systemsRoot);
        }

        private static List<string> EnumerateExternalImages()
        {
            if (!Directory.Exists(ExternalFixturesPath))
            {
                return new List<string>();
            }

            return Directory.GetFiles(ExternalFixturesPath)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f)?.ToLowerInvariant();
                    return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
                })
                .ToList();
        }

        private static int CountNormalizableBoxes(DetectionResult detection, int width, int height)
        {
            if (detection?.bboxes == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var bbox in detection.bboxes)
            {
                if (BoundingBoxUtils.TryNormalize(bbox, width, height, out _))
                {
                    count++;
                }
            }

            return count;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Assert.Fail("Missing field: " + fieldName);
            }
            field.SetValue(target, value);
        }
    }
}
