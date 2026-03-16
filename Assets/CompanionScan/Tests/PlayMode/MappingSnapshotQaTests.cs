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
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CompanionScan.Tests.PlayMode
{
    public class MappingSnapshotQaTests
    {
        private const string ServerUrlEnv = "CompanionScan_TEST_SERVER_URL";

        private static readonly string ExternalFixturesPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "test"));

        private ServerConfig _config;
        private CVServerClient _client;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            TestRuntimeFlags.RequireLiveIntegrationOrIgnore("Live QA suite disabled");
            _config = ScriptableObject.CreateInstance<ServerConfig>();

            var envUrl = Environment.GetEnvironmentVariable(ServerUrlEnv);
            _config.baseUrl = string.IsNullOrWhiteSpace(envUrl)
                ? _config.ResolveBaseUrl()
                : envUrl;

            if (string.IsNullOrWhiteSpace(_config.baseUrl))
            {
                Assert.Ignore($"Set {ServerUrlEnv} or ServerConfig.baseUrl to run QA tests.");
            }

            _client = new CVServerClient(_config);
        }

        [UnityTest]
        public IEnumerator TestImages_RunModels_RenderBoxes_And_RaycastAnchors()
        {
            var images = EnumerateExternalImages();
            if (images.Count == 0)
            {
                Assert.Inconclusive($"No jpg/png fixtures found at {ExternalFixturesPath}");
            }

            foreach (var path in images)
            {
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[QA] {Path.GetFileName(path)} read failed: {ex.Message}");
                    continue;
                }

                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!tex.LoadImage(bytes))
                {
                    Debug.LogWarning($"[QA] {Path.GetFileName(path)} could not be decoded as Texture2D.");
                    UnityEngine.Object.DestroyImmediate(tex);
                    continue;
                }

                // Florence
                var florenceTask = _client.FlorenceAsync(bytes, "<MORE_DETAILED_CAPTION>");
                while (!florenceTask.IsCompleted) yield return null;
                if (florenceTask.IsFaulted)
                {
                    Assert.Fail(florenceTask.Exception?.GetBaseException().Message);
                }
                Assert.IsNotNull(florenceTask.Result);

                // GroundingDINO
                var detectTask = _client.DetectAsync(bytes, "object . furniture . shelf .");
                while (!detectTask.IsCompleted) yield return null;
                if (detectTask.IsFaulted)
                {
                    Assert.Fail(detectTask.Exception?.GetBaseException().Message);
                }
                var detection = detectTask.Result;
                Assert.IsNotNull(detection);

                // SAM2
                string samPrompt = detection.labels != null && detection.labels.Count > 0
                    ? string.Join(" . ", detection.labels.Take(6)) + " ."
                    : "object .";
                var samTask = _client.SegmentAsync(bytes, samPrompt);
                while (!samTask.IsCompleted) yield return null;
                if (samTask.IsFaulted)
                {
                    Assert.Fail(samTask.Exception?.GetBaseException().Message);
                }
                Assert.IsNotNull(samTask.Result);

                // Render 2D boxes
                var uiRoot = new GameObject("BBoxUiRoot");
                var canvas = uiRoot.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var containerGo = new GameObject("BBoxContainer", typeof(RectTransform));
                containerGo.transform.SetParent(uiRoot.transform, false);
                var containerRt = containerGo.GetComponent<RectTransform>();
                containerRt.sizeDelta = new Vector2(tex.width, tex.height);

                var bboxPrefab = BuildBBoxPrefab();

                var bboxRenderer = new GameObject("BBoxRenderer").AddComponent<BBoxRenderer>();
                SetPrivateField(bboxRenderer, "container", containerRt);
                SetPrivateField(bboxRenderer, "bboxPrefab", bboxPrefab);

                bboxRenderer.RenderBoxes(detection, tex);

                int expectedBoxes = CountNormalizableBoxes(detection, tex.width, tex.height);
                Assert.AreEqual(expectedBoxes, containerRt.childCount,
                    $"Expected {expectedBoxes} bbox visuals for {Path.GetFileName(path)}");

                // Raycast 3D anchors
                var cameraGo = new GameObject("TestCamera", typeof(Camera));
                cameraGo.tag = "MainCamera";
                cameraGo.transform.position = Vector3.zero;
                cameraGo.transform.rotation = Quaternion.identity;

                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.transform.position = new Vector3(0f, 0f, 2f);
                plane.transform.localScale = Vector3.one * 4f;

                var raycaster = new GameObject("Raycaster").AddComponent<ViewportRaycaster>();
                raycaster.FallbackDistance = 2f;

                int anchors = 0;
                foreach (var bbox in detection.bboxes)
                {
                    var validation = BoundingBoxUtils.Validate(bbox, tex.width, tex.height, BoundingBoxValidationOptions.ForRendering(1f), 0);
                    if (!validation.IsValid)
                    {
                        continue;
                    }
                    Rect norm = validation.NormalizedRect;
                    float u = norm.x + norm.width * 0.5f;
                    float v = 1f - (norm.y + norm.height * 0.5f);

                    if (!raycaster.TryRaycastViewport(new Vector2(u, v), out Vector3 hitPoint, out _))
                    {
                        hitPoint = cameraGo.transform.position + cameraGo.transform.forward * raycaster.FallbackDistance;
                    }

                    var anchor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    anchor.transform.position = hitPoint;
                    anchor.transform.localScale = Vector3.one * 0.05f;
                    anchors++;
                }

                Assert.AreEqual(expectedBoxes, anchors,
                    $"Expected {expectedBoxes} anchor placements for {Path.GetFileName(path)}");

                UnityEngine.Object.DestroyImmediate(uiRoot);
                UnityEngine.Object.DestroyImmediate(bboxPrefab);
                UnityEngine.Object.DestroyImmediate(bboxRenderer.gameObject);
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(plane);
                UnityEngine.Object.DestroyImmediate(raycaster.gameObject);
                UnityEngine.Object.DestroyImmediate(tex);
            }
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

        private static GameObject BuildBBoxPrefab()
        {
            var root = new GameObject("BBoxPrefab", typeof(RectTransform));
            root.AddComponent<Image>();
            return root;
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
                var validation = BoundingBoxUtils.Validate(bbox, width, height, BoundingBoxValidationOptions.ForRendering(1f), 0);
                if (validation.IsValid)
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
                throw new MissingFieldException(target.GetType().Name, fieldName);
            }
            field.SetValue(target, value);
        }
    }
}


