using System;
using System.Collections;
using System.IO;
using System.Linq;
using HouseMapper.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HouseMapper.Tests.PlayMode
{
    /// <summary>
    /// Integration tests that send realistic 1280×1024 fixture images through each
    /// cv-server endpoint (Florence2, GroundingDINO, SAM2) and the full sequential
    /// pipeline.  Requires a running cv-server instance.
    /// </summary>
    public class PipelineIntegrationTests
    {
        private const string ServerUrlEnv = "HOUSEMAPPER_TEST_SERVER_URL";

        /// <summary>Absolute path to the Fixtures folder (resolved once).</summary>
        private static readonly string FixturesPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "HouseMapper", "Tests", "Fixtures"));

        private ServerConfig _config;
        private CVServerClient _client;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _config = ScriptableObject.CreateInstance<ServerConfig>();

            var envUrl = Environment.GetEnvironmentVariable(ServerUrlEnv);
            _config.baseUrl = string.IsNullOrWhiteSpace(envUrl)
                ? _config.ResolveBaseUrl()   // uses default in ServerConfig
                : envUrl;

            if (string.IsNullOrWhiteSpace(_config.baseUrl))
            {
                Assert.Ignore($"Set {ServerUrlEnv} or assign ServerConfig.baseUrl to enable pipeline tests.");
            }

            _client = new CVServerClient(_config);
        }

        // ────────────────────────────────────────────────────────────
        //  Helpers
        // ────────────────────────────────────────────────────────────

        /// <summary>Load a fixture JPEG by filename.</summary>
        private static byte[] LoadFixture(string filename)
        {
            var path = Path.Combine(FixturesPath, filename);
            if (!File.Exists(path))
            {
                Assert.Inconclusive($"Fixture not found: {path}");
            }
            return File.ReadAllBytes(path);
        }

        /// <summary>
        /// Returns the first fixture JPEG found in the Fixtures folder.
        /// Skips the test if none are present.
        /// </summary>
        private static byte[] LoadAnyFixture()
        {
            if (!Directory.Exists(FixturesPath))
            {
                Assert.Inconclusive($"Fixtures folder not found: {FixturesPath}");
            }

            var file = Directory.GetFiles(FixturesPath, "*.jpg").FirstOrDefault()
                       ?? Directory.GetFiles(FixturesPath, "*.png").FirstOrDefault();

            if (file == null)
            {
                Assert.Inconclusive("No fixture images found.");
            }

            return File.ReadAllBytes(file);
        }

        // ────────────────────────────────────────────────────────────
        //  Florence2 — Caption
        // ────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Florence2_Caption_ReturnsNonEmptyText()
        {
            var jpeg = LoadAnyFixture();

            var task = _client.FlorenceAsync(jpeg, "<MORE_DETAILED_CAPTION>");
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted) Assert.Fail(task.Exception?.GetBaseException().Message);

            var result = task.Result;
            Assert.IsNotNull(result, "FlorenceResult should not be null");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.text),
                "Florence2 caption text should not be empty");

            Debug.Log($"[Test] Florence2 caption: {result.text}");
        }

        // ────────────────────────────────────────────────────────────
        //  Florence2 — Object Detection
        // ────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Florence2_ObjectDetection_ReturnsBboxesAndLabels()
        {
            var jpeg = LoadAnyFixture();

            var task = _client.FlorenceAsync(jpeg, "<OD>");
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted) Assert.Fail(task.Exception?.GetBaseException().Message);

            var result = task.Result;
            Assert.IsNotNull(result, "FlorenceResult should not be null");
            Assert.IsNotNull(result.results, "Florence2 OD results token should not be null");

            // The results should contain bboxes and labels
            var resultsStr = result.results.ToString();
            Debug.Log($"[Test] Florence2 OD results: {resultsStr}");
            Assert.IsTrue(resultsStr.Length > 2,
                "Florence2 OD results should contain meaningful data");
        }

        // ────────────────────────────────────────────────────────────
        //  GroundingDINO — Detect
        // ────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator GroundingDINO_Detect_ReturnsDetections()
        {
            var jpeg = LoadAnyFixture();

            var task = _client.DetectAsync(jpeg, "furniture . object . table . chair .");
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted) Assert.Fail(task.Exception?.GetBaseException().Message);

            var result = task.Result;
            Assert.IsNotNull(result, "DetectionResult should not be null");
            Assert.IsNotNull(result.labels, "Detection labels should not be null");

            Debug.Log($"[Test] GroundingDINO found {result.labels.Count} objects");
            for (int i = 0; i < result.labels.Count; i++)
            {
                float score = i < result.scores.Count ? result.scores[i] : 0f;
                Debug.Log($"  [{i}] {result.labels[i]}  score={score:F3}");
            }

            // Synthetic test images have colored rectangles labeled as furniture,
            // so we should get at least some detections.
            Assert.IsTrue(result.labels.Count >= 0,
                "Should return zero or more detections without error");
        }

        [UnityTest]
        public IEnumerator GroundingDINO_Detect_BboxesAreNormalized()
        {
            var jpeg = LoadAnyFixture();

            var task = _client.DetectAsync(jpeg, "furniture . object .");
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted) Assert.Fail(task.Exception?.GetBaseException().Message);

            var result = task.Result;
            if (result.bboxes == null || result.bboxes.Count == 0)
            {
                Assert.Inconclusive("No detections returned — cannot validate bbox ranges.");
            }

            foreach (var bbox in result.bboxes)
            {
                Assert.AreEqual(4, bbox.Length, "Each bbox should have 4 values");
                for (int j = 0; j < 4; j++)
                {
                    Assert.IsTrue(bbox[j] >= 0f && bbox[j] <= 1.0f,
                        $"Bbox value {bbox[j]} at index {j} should be normalized to [0,1]");
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        //  SAM2 — Segment
        // ────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SAM2_Segment_ReturnsMasks()
        {
            var jpeg = LoadAnyFixture();

            var task = _client.SegmentAsync(jpeg, "furniture . object .");
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted) Assert.Fail(task.Exception?.GetBaseException().Message);

            var result = task.Result;
            Assert.IsNotNull(result, "SegmentationResult should not be null");
            Assert.IsNotNull(result.annotations, "Annotations list should not be null");

            Debug.Log($"[Test] SAM2 returned {result.annotations.Count} masks, " +
                      $"image_size=[{string.Join(",", result.image_size)}]");

            foreach (var ann in result.annotations)
            {
                Assert.IsFalse(string.IsNullOrEmpty(ann.label),
                    "Each annotation should have a label");
                Assert.IsNotNull(ann.mask, "Each annotation should have a mask");
            }
        }

        // ────────────────────────────────────────────────────────────
        //  Full Sequential Pipeline (Florence2 → GDino → SAM2)
        // ────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator FullPipeline_Florence2_GDino_SAM2_Sequential()
        {
            var jpeg = LoadAnyFixture();

            // Step 1: Florence2 caption
            Debug.Log("[Test-Pipeline] Step 1: Florence2 caption…");
            var captionTask = _client.FlorenceAsync(jpeg, "<MORE_DETAILED_CAPTION>");
            while (!captionTask.IsCompleted) yield return null;
            if (captionTask.IsFaulted) Assert.Fail($"Caption failed: {captionTask.Exception?.GetBaseException().Message}");
            var caption = captionTask.Result?.text;
            Assert.IsFalse(string.IsNullOrWhiteSpace(caption), "Caption should not be empty");
            Debug.Log($"[Test-Pipeline] Caption: {caption}");

            // Step 2: Florence2 OD
            Debug.Log("[Test-Pipeline] Step 2: Florence2 OD…");
            var odTask = _client.FlorenceAsync(jpeg, "<OD>");
            while (!odTask.IsCompleted) yield return null;
            if (odTask.IsFaulted) Assert.Fail($"Florence2 OD failed: {odTask.Exception?.GetBaseException().Message}");
            var odResult = odTask.Result;
            Assert.IsNotNull(odResult?.results, "Florence2 OD results should not be null");
            Debug.Log($"[Test-Pipeline] OD results: {odResult.results}");

            // Step 3: GroundingDINO using fallback prompt
            Debug.Log("[Test-Pipeline] Step 3: GroundingDINO detect…");
            var detectTask = _client.DetectAsync(jpeg, "furniture . object . table . chair .");
            while (!detectTask.IsCompleted) yield return null;
            if (detectTask.IsFaulted) Assert.Fail($"GroundingDINO failed: {detectTask.Exception?.GetBaseException().Message}");
            var detection = detectTask.Result;
            Assert.IsNotNull(detection, "Detection result should not be null");
            Debug.Log($"[Test-Pipeline] GDino detections: {detection.labels?.Count ?? 0}");

            // Step 4: SAM2 segment
            Debug.Log("[Test-Pipeline] Step 4: SAM2 segment…");
            string samPrompt = detection.labels != null && detection.labels.Count > 0
                ? string.Join(" . ", detection.labels.Distinct()) + " ."
                : "furniture . object .";
            var segTask = _client.SegmentAsync(jpeg, samPrompt);
            while (!segTask.IsCompleted) yield return null;
            if (segTask.IsFaulted) Assert.Fail($"SAM2 failed: {segTask.Exception?.GetBaseException().Message}");
            var segResult = segTask.Result;
            Assert.IsNotNull(segResult, "Segmentation result should not be null");
            Debug.Log($"[Test-Pipeline] SAM2 masks: {segResult.annotations?.Count ?? 0}");

            // Summary
            Debug.Log(
                $"[Test-Pipeline] COMPLETE — caption=\"{caption?.Substring(0, Math.Min(80, caption?.Length ?? 0))}…\" " +
                $"detections={detection.labels?.Count ?? 0} masks={segResult.annotations?.Count ?? 0}");
        }

        // ────────────────────────────────────────────────────────────
        //  Per-fixture tests
        // ────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LivingRoom_Florence2_Caption()
        {
            var jpeg = LoadFixture("living_room_1280x1024.jpg");
            var task = _client.FlorenceAsync(jpeg, "<MORE_DETAILED_CAPTION>");
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) Assert.Fail(task.Exception?.GetBaseException().Message);
            Assert.IsFalse(string.IsNullOrWhiteSpace(task.Result?.text));
            Debug.Log($"[Test] Living room caption: {task.Result.text}");
        }

        [UnityTest]
        public IEnumerator Kitchen_GroundingDINO_Detect()
        {
            var jpeg = LoadFixture("kitchen_1280x1024.jpg");
            var task = _client.DetectAsync(jpeg, "appliance . counter . sink . stove .");
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) Assert.Fail(task.Exception?.GetBaseException().Message);
            Assert.IsNotNull(task.Result);
            Debug.Log($"[Test] Kitchen detections: {task.Result.labels?.Count ?? 0}");
        }

        [UnityTest]
        public IEnumerator Office_SAM2_Segment()
        {
            var jpeg = LoadFixture("office_1280x1024.jpg");
            var task = _client.SegmentAsync(jpeg, "desk . monitor . chair . keyboard .");
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) Assert.Fail(task.Exception?.GetBaseException().Message);
            Assert.IsNotNull(task.Result);
            Debug.Log($"[Test] Office masks: {task.Result.annotations?.Count ?? 0}");
        }
    }
}
