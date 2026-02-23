using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HouseMapper.Core;
using HouseMapper.Mapping;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HouseMapper.Tests.PlayMode
{
    /// <summary>
    /// QA integration tests that send real photographs from the /test folder
    /// through each cv-server model (Florence2, GroundingDINO, SAM2) and verify
    /// the results contain valid 2D bounding boxes and data suitable for 3D
    /// anchor placement.
    ///
    /// These tests require a live cv-server instance.  Set the environment
    /// variable <c>HOUSEMAPPER_TEST_SERVER_URL</c>, or rely on the default
    /// <see cref="ServerConfig.baseUrl"/>.
    /// </summary>
    public class PerImageModelTests
    {
        // ────────────────────────────────────────────────────────────
        //  Constants & fixture data
        // ────────────────────────────────────────────────────────────

        private const string ServerUrlEnv = "HOUSEMAPPER_TEST_SERVER_URL";

        private static readonly string FixturesPath =
            Path.GetFullPath(Path.Combine(Application.dataPath,
                "HouseMapper", "Tests", "Fixtures"));

        /// <summary>Image → (prompt for GDino/SAM2, description)</summary>
        private static readonly (string file, string prompt, string desc)[] TestImages =
        {
            ("closet2.jpg",                "clothing . shelf . hanger . box .",       "closet"),
            ("closet3.jpeg",               "clothing . shelf . hanger . box .",       "closet"),
            ("food-storage-containers.jpg", "container . jar . lid . shelf .",        "food containers"),
            ("food-storage-jars.jpg",      "jar . bottle . label . shelf .",          "food jars"),
            ("How-To-Organize-ACloset.jpg","shelf . bin . box . basket . clothing .", "organized closet"),
            ("laundry-room1.jpg",          "washer . dryer . shelf . basket .",       "laundry room"),
            ("laundry-room2.jpg",          "washer . dryer . shelf . basket .",       "laundry room"),
            ("pantry-organization.jpg",    "shelf . jar . container . can . box .",   "pantry"),
            ("pantry-organization2.jpg",   "shelf . jar . container . can . box .",   "pantry"),
            ("pantry.jpg",                 "shelf . jar . container . can . box .",   "pantry"),
            ("walk-in-pantry-design.jpg",  "shelf . jar . container . can . box .",   "walk-in pantry"),
            ("kitchen_1280x1024.jpg",      "appliance . counter . sink . stove .",    "kitchen (synth)"),
            ("living_room_1280x1024.jpg",  "sofa . table . lamp . chair .",           "living room (synth)"),
            ("office_1280x1024.jpg",       "desk . monitor . chair . keyboard .",     "office (synth)"),
        };

        private ServerConfig _config;
        private CVServerClient _client;

        // ────────────────────────────────────────────────────────────
        //  Setup
        // ────────────────────────────────────────────────────────────

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _config = ScriptableObject.CreateInstance<ServerConfig>();

            var envUrl = Environment.GetEnvironmentVariable(ServerUrlEnv);
            _config.baseUrl = string.IsNullOrWhiteSpace(envUrl)
                ? _config.ResolveBaseUrl()
                : envUrl;

            if (string.IsNullOrWhiteSpace(_config.baseUrl))
                Assert.Ignore($"Set {ServerUrlEnv} or ServerConfig.baseUrl to run model tests.");

            _client = new CVServerClient(_config);

            if (!Directory.Exists(FixturesPath))
                Assert.Inconclusive($"Fixtures directory missing: {FixturesPath}");
        }

        // ────────────────────────────────────────────────────────────
        //  Helpers
        // ────────────────────────────────────────────────────────────

        private static byte[] LoadFixture(string filename)
        {
            var path = Path.Combine(FixturesPath, filename);
            if (!File.Exists(path))
                Assert.Inconclusive($"Fixture not found: {path}");
            return File.ReadAllBytes(path);
        }

        /// <summary>
        /// Simulate 3D anchor placement for each detection bbox.
        /// Returns computed (position, label) pairs as if raycasted at each bbox center.
        /// Uses a simple projection: bbox center UV → Camera.main viewport ray at fallback distance.
        /// </summary>
        private static List<(Vector3 pos, string label)> SimulateAnchorPlacement(
            DetectionResult detection, int imageWidth, int imageHeight, float fallbackDist = 2f)
        {
            var results = new List<(Vector3, string)>();
            if (detection?.bboxes == null) return results;

            int count = Mathf.Min(detection.bboxes.Count, detection.labels.Count);
            for (int i = 0; i < count; i++)
            {
                var bbox = detection.bboxes[i];
                if (bbox == null || bbox.Length < 4) continue;

                if (!BoundingBoxUtils.TryNormalize(bbox, imageWidth, imageHeight, out Rect norm))
                    continue;

                float u = norm.x + norm.width * 0.5f;
                float v = 1f - (norm.y + norm.height * 0.5f);

                // Simulate 3D position (no real raycast available in test runner)
                var pos = new Vector3(
                    (u - 0.5f) * fallbackDist * 2f,  // spread X
                    (v - 0.5f) * fallbackDist * 2f,  // spread Y
                    fallbackDist                       // fixed depth
                );

                results.Add((pos, detection.labels[i]));
            }
            return results;
        }

        // ════════════════════════════════════════════════════════════════
        //  FLORENCE2 – Caption per image
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator Florence2_Caption_AllImages()
        {
            int passed = 0;
            foreach (var (file, _, desc) in TestImages)
            {
                byte[] jpeg;
                try { jpeg = LoadFixture(file); }
                catch { continue; }

                var task = _client.FlorenceAsync(jpeg, "<MORE_DETAILED_CAPTION>");
                while (!task.IsCompleted) yield return null;

                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[Florence2-Caption] {file}: FAILED – {task.Exception?.GetBaseException().Message}");
                    continue;
                }

                var result = task.Result;
                Assert.IsNotNull(result, $"Null result for {file}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.text),
                    $"Empty caption for {file}");

                string snippet = result.text.Length > 100
                    ? result.text.Substring(0, 100) + "…"
                    : result.text;
                Debug.Log($"[Florence2-Caption] {desc} ({file}): {snippet}");
                passed++;
            }
            Debug.Log($"[Florence2-Caption] {passed}/{TestImages.Length} images captioned successfully.");
            Assert.IsTrue(passed > 0, "At least one image should produce a caption.");
        }

        // ════════════════════════════════════════════════════════════════
        //  FLORENCE2 – Object Detection per image
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator Florence2_OD_AllImages()
        {
            int passed = 0;
            foreach (var (file, _, desc) in TestImages)
            {
                byte[] jpeg;
                try { jpeg = LoadFixture(file); }
                catch { continue; }

                var task = _client.FlorenceAsync(jpeg, "<OD>");
                while (!task.IsCompleted) yield return null;

                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[Florence2-OD] {file}: FAILED – {task.Exception?.GetBaseException().Message}");
                    continue;
                }

                var result = task.Result;
                Assert.IsNotNull(result, $"Null result for {file}");

                Debug.Log($"[Florence2-OD] {desc} ({file}): task={result.task} text_len={result.text?.Length ?? 0}");
                passed++;
            }
            Debug.Log($"[Florence2-OD] {passed}/{TestImages.Length} images detected successfully.");
            Assert.IsTrue(passed > 0, "At least one image should return OD results.");
        }

        // ════════════════════════════════════════════════════════════════
        //  GROUNDING DINO – Detection + 2D bbox validation
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator GDino_Detect_AllImages_BboxesValid()
        {
            int totalDetections = 0;
            int imagesPassed = 0;

            foreach (var (file, prompt, desc) in TestImages)
            {
                byte[] jpeg;
                try { jpeg = LoadFixture(file); }
                catch { continue; }

                var task = _client.DetectAsync(jpeg, prompt);
                while (!task.IsCompleted) yield return null;

                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[GDino] {file}: FAILED – {task.Exception?.GetBaseException().Message}");
                    continue;
                }

                var det = task.Result;
                Assert.IsNotNull(det, $"Null DetectionResult for {file}");
                Assert.IsNotNull(det.labels, $"Null labels for {file}");
                Assert.IsNotNull(det.bboxes, $"Null bboxes for {file}");

                // Validate every bbox
                for (int i = 0; i < det.bboxes.Count; i++)
                {
                    var bbox = det.bboxes[i];
                    Assert.AreEqual(4, bbox.Length,
                        $"{file} bbox[{i}] should have 4 values");

                    // Bboxes from /v1/detect are absolute pixel coords or normalised
                    // TryNormalize handles both
                    Assert.IsTrue(
                        BoundingBoxUtils.TryNormalize(bbox, 1280, 1024, out _),
                        $"{file} bbox[{i}] could not be normalised");
                }

                int n = det.labels.Count;
                totalDetections += n;
                imagesPassed++;

                // Log top-3 detections
                var top3 = det.labels.Take(3).ToArray();
                Debug.Log($"[GDino] {desc} ({file}): {n} detections – [{string.Join(", ", top3)}{(n > 3 ? "…" : "")}]");
            }

            Debug.Log($"[GDino] {imagesPassed}/{TestImages.Length} images, {totalDetections} total detections.");
            Assert.IsTrue(imagesPassed > 0, "At least one image should have detections.");
        }

        // ════════════════════════════════════════════════════════════════
        //  GROUNDING DINO – 3D anchor simulation
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator GDino_Detect_AllImages_Anchors3D()
        {
            int totalAnchors = 0;

            foreach (var (file, prompt, desc) in TestImages)
            {
                byte[] jpeg;
                try { jpeg = LoadFixture(file); }
                catch { continue; }

                var task = _client.DetectAsync(jpeg, prompt);
                while (!task.IsCompleted) yield return null;

                if (task.IsFaulted) continue;

                var det = task.Result;
                if (det?.bboxes == null || det.bboxes.Count == 0) continue;

                // Simulate 3D anchor creation from bbox centres
                var anchors = SimulateAnchorPlacement(det, 1280, 1024);

                foreach (var (pos, label) in anchors)
                {
                    // Verify position is finite
                    Assert.IsFalse(float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z),
                        $"Anchor position for '{label}' in {file} has NaN");
                    Assert.IsFalse(float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z),
                        $"Anchor position for '{label}' in {file} has Infinity");
                }

                totalAnchors += anchors.Count;
                Debug.Log($"[GDino-3D] {desc} ({file}): {anchors.Count} 3D anchors computed");
            }

            Debug.Log($"[GDino-3D] Total 3D anchors across all images: {totalAnchors}");
            Assert.IsTrue(totalAnchors > 0, "Should compute at least one 3D anchor position.");
        }

        // ════════════════════════════════════════════════════════════════
        //  SAM2 – Segmentation + mask validation
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator SAM2_Segment_AllImages_MasksValid()
        {
            int totalMasks = 0;
            int imagesPassed = 0;

            foreach (var (file, prompt, desc) in TestImages)
            {
                byte[] jpeg;
                try { jpeg = LoadFixture(file); }
                catch { continue; }

                var task = _client.SegmentAsync(jpeg, prompt);
                while (!task.IsCompleted) yield return null;

                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[SAM2] {file}: FAILED – {task.Exception?.GetBaseException().Message}");
                    continue;
                }

                var seg = task.Result;
                Assert.IsNotNull(seg, $"Null SegmentationResult for {file}");
                Assert.IsNotNull(seg.annotations, $"Null annotations for {file}");

                // Validate image_size
                Assert.IsNotNull(seg.image_size, $"image_size null for {file}");
                Assert.AreEqual(2, seg.image_size.Count,
                    $"image_size should have [h,w] for {file}");

                foreach (var ann in seg.annotations)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(ann.label),
                        $"Empty label in annotation for {file}");
                    Assert.IsTrue(ann.score > 0f,
                        $"Score should be > 0 for {file}/{ann.label}");

                    // Validate bbox
                    Assert.IsNotNull(ann.bbox, $"Null bbox for {file}/{ann.label}");
                    Assert.AreEqual(4, ann.bbox.Length,
                        $"bbox should have 4 values for {file}/{ann.label}");

                    // Validate mask
                    Assert.IsNotNull(ann.mask,
                        $"Null mask for {file}/{ann.label}");
                    Assert.IsNotNull(ann.mask.counts,
                        $"Null mask.counts for {file}/{ann.label}");
                    Assert.IsNotNull(ann.mask.size,
                        $"Null mask.size for {file}/{ann.label}");
                    Assert.AreEqual(2, ann.mask.size.Count,
                        $"mask.size should be [h,w] for {file}/{ann.label}");
                }

                totalMasks += seg.annotations.Count;
                imagesPassed++;

                var labels = seg.annotations.Select(a => a.label).Distinct().ToArray();
                Debug.Log($"[SAM2] {desc} ({file}): {seg.annotations.Count} masks – [{string.Join(", ", labels)}]");
            }

            Debug.Log($"[SAM2] {imagesPassed}/{TestImages.Length} images, {totalMasks} total masks.");
            Assert.IsTrue(imagesPassed > 0, "At least one image should produce masks.");
        }

        // ════════════════════════════════════════════════════════════════
        //  SAM2 – 3D anchor simulation from segment annotations
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator SAM2_Segment_AllImages_Anchors3D()
        {
            int totalAnchors = 0;

            foreach (var (file, prompt, desc) in TestImages)
            {
                byte[] jpeg;
                try { jpeg = LoadFixture(file); }
                catch { continue; }

                var task = _client.SegmentAsync(jpeg, prompt);
                while (!task.IsCompleted) yield return null;

                if (task.IsFaulted) continue;

                var seg = task.Result;
                if (seg?.annotations == null || seg.annotations.Count == 0) continue;

                int h = seg.image_size[0];
                int w = seg.image_size[1];

                foreach (var ann in seg.annotations)
                {
                    if (ann.bbox == null || ann.bbox.Length < 4) continue;
                    if (!BoundingBoxUtils.TryNormalize(ann.bbox, w, h, out Rect norm))
                        continue;

                    float u = norm.x + norm.width * 0.5f;
                    float v = 1f - (norm.y + norm.height * 0.5f);

                    var pos = new Vector3(
                        (u - 0.5f) * 4f,
                        (v - 0.5f) * 4f,
                        2f
                    );

                    Assert.IsFalse(float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z),
                        $"NaN anchor for {file}/{ann.label}");

                    totalAnchors++;
                }

                Debug.Log($"[SAM2-3D] {desc} ({file}): {seg.annotations.Count} 3D anchors");
            }

            Debug.Log($"[SAM2-3D] Total 3D anchors: {totalAnchors}");
            Assert.IsTrue(totalAnchors > 0, "Should compute at least one 3D anchor from SAM2 masks.");
        }

        // ════════════════════════════════════════════════════════════════
        //  FULL PIPELINE – Florence2 → GDino → SAM2 per image
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator FullPipeline_PerImage_AllThreeModels()
        {
            int imagesPassed = 0;

            foreach (var (file, fallbackPrompt, desc) in TestImages)
            {
                byte[] jpeg;
                try { jpeg = LoadFixture(file); }
                catch { continue; }

                Debug.Log($"\n[Pipeline] ═══ {desc} ({file}) ═══");

                // Step 1: Florence2 caption
                var capTask = _client.FlorenceAsync(jpeg, "<MORE_DETAILED_CAPTION>");
                while (!capTask.IsCompleted) yield return null;
                if (capTask.IsFaulted)
                {
                    Debug.LogWarning($"[Pipeline] {file} Florence2 caption failed: {capTask.Exception?.GetBaseException().Message}");
                    continue;
                }
                var caption = capTask.Result?.text ?? "(no caption)";
                Debug.Log($"[Pipeline] Caption: {(caption.Length > 80 ? caption.Substring(0, 80) + "…" : caption)}");

                // Step 2: Florence2 OD → extract labels
                var odTask = _client.FlorenceAsync(jpeg, "<OD>");
                while (!odTask.IsCompleted) yield return null;
                string dinoPrompt = fallbackPrompt;
                if (!odTask.IsFaulted && odTask.Result?.text != null)
                {
                    Debug.Log($"[Pipeline] Florence2 OD text: {odTask.Result.text.Substring(0, Math.Min(80, odTask.Result.text.Length))}");
                }

                // Step 3: GroundingDINO
                var detTask = _client.DetectAsync(jpeg, dinoPrompt);
                while (!detTask.IsCompleted) yield return null;
                if (detTask.IsFaulted)
                {
                    Debug.LogWarning($"[Pipeline] {file} GDino failed: {detTask.Exception?.GetBaseException().Message}");
                    continue;
                }
                var det = detTask.Result;
                Assert.IsNotNull(det, $"Null detection for {file}");
                int detCount = det.labels?.Count ?? 0;
                Debug.Log($"[Pipeline] GDino: {detCount} detections");

                // Validate 2D bboxes
                for (int i = 0; i < (det.bboxes?.Count ?? 0); i++)
                {
                    Assert.IsTrue(BoundingBoxUtils.TryNormalize(det.bboxes[i], 1280, 1024, out _),
                        $"bbox[{i}] normalisation failed for {file}");
                }

                // Simulate 3D anchor placement
                var anchors3D = SimulateAnchorPlacement(det, 1280, 1024);
                Debug.Log($"[Pipeline] 3D anchors from GDino: {anchors3D.Count}");

                // Step 4: SAM2
                string samPrompt = det.labels != null && det.labels.Count > 0
                    ? string.Join(" . ", det.labels.Distinct()) + " ."
                    : fallbackPrompt;
                var segTask = _client.SegmentAsync(jpeg, samPrompt);
                while (!segTask.IsCompleted) yield return null;
                if (segTask.IsFaulted)
                {
                    Debug.LogWarning($"[Pipeline] {file} SAM2 failed: {segTask.Exception?.GetBaseException().Message}");
                    continue;
                }
                var seg = segTask.Result;
                Assert.IsNotNull(seg, $"Null segmentation for {file}");
                int maskCount = seg.annotations?.Count ?? 0;
                Debug.Log($"[Pipeline] SAM2: {maskCount} masks");

                Debug.Log($"[Pipeline] ✓ {desc}: caption=OK, det={detCount}, anchors={anchors3D.Count}, masks={maskCount}");
                imagesPassed++;
            }

            Debug.Log($"\n[Pipeline] ═══ SUMMARY: {imagesPassed}/{TestImages.Length} images passed full pipeline ═══");
            Assert.IsTrue(imagesPassed > 0, "At least one image should pass the full pipeline.");
        }

        // ════════════════════════════════════════════════════════════════
        //  2D BBOX rendering data validation
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator BBox2D_RenderingData_ValidForAllDetections()
        {
            foreach (var (file, prompt, desc) in TestImages)
            {
                byte[] jpeg;
                try { jpeg = LoadFixture(file); }
                catch { continue; }

                var task = _client.DetectAsync(jpeg, prompt);
                while (!task.IsCompleted) yield return null;
                if (task.IsFaulted) continue;

                var det = task.Result;
                if (det?.bboxes == null) continue;

                for (int i = 0; i < det.bboxes.Count; i++)
                {
                    var bbox = det.bboxes[i];
                    Assert.IsTrue(BoundingBoxUtils.TryNormalize(bbox, 1280, 1024, out Rect norm),
                        $"bbox normalisation failed for {file}[{i}]");

                    // Verify the normalised rect has positive dimensions
                    Assert.IsTrue(norm.width > 0f, $"bbox width <= 0 for {file}[{i}]");
                    Assert.IsTrue(norm.height > 0f, $"bbox height <= 0 for {file}[{i}]");

                    // Verify it's within [0,1] bounds (clamped)
                    Assert.IsTrue(norm.x >= 0f && norm.x <= 1f, $"bbox x out of [0,1] for {file}[{i}]");
                    Assert.IsTrue(norm.y >= 0f && norm.y <= 1f, $"bbox y out of [0,1] for {file}[{i}]");

                    // Convert to pixel coords (simulating what BBoxRenderer does)
                    float containerW = 1280f;
                    float containerH = 1024f;
                    float x1 = norm.x * containerW;
                    float y1 = norm.y * containerH;
                    float x2 = (norm.x + norm.width) * containerW;
                    float y2 = (norm.y + norm.height) * containerH;

                    Assert.IsTrue(x2 > x1, $"pixel x2 <= x1 for {file}[{i}]");
                    Assert.IsTrue(y2 > y1, $"pixel y2 <= y1 for {file}[{i}]");

                    float sizeW = Mathf.Abs(x2 - x1);
                    float sizeH = Mathf.Abs(y2 - y1);
                    Assert.IsTrue(sizeW > 1f, $"bbox too narrow ({sizeW}px) for {file}[{i}]");
                    Assert.IsTrue(sizeH > 1f, $"bbox too short ({sizeH}px) for {file}[{i}]");
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  3D ANCHOR data validation – SpatialPin-compatible
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator Anchor3D_PinData_ValidForAllDetections()
        {
            foreach (var (file, prompt, desc) in TestImages)
            {
                byte[] jpeg;
                try { jpeg = LoadFixture(file); }
                catch { continue; }

                var task = _client.DetectAsync(jpeg, prompt);
                while (!task.IsCompleted) yield return null;
                if (task.IsFaulted) continue;

                var det = task.Result;
                if (det?.bboxes == null) continue;

                int count = Mathf.Min(det.bboxes.Count, det.labels.Count);
                for (int i = 0; i < count; i++)
                {
                    float score = i < det.scores.Count ? det.scores[i] : 0f;
                    var bbox = det.bboxes[i];
                    if (bbox == null || bbox.Length < 4) continue;

                    if (!BoundingBoxUtils.TryNormalize(bbox, 1280, 1024, out Rect norm))
                        continue;

                    // Validate data that would go into a SpatialPin
                    Assert.IsFalse(string.IsNullOrEmpty(det.labels[i]),
                        $"Empty label at [{i}] for {file}");
                    Assert.IsTrue(score >= 0f && score <= 1f,
                        $"Score {score} out of [0,1] for {file}[{i}]");

                    // Bbox centre UV for raycasting
                    float u = norm.x + norm.width * 0.5f;
                    float v = 1f - (norm.y + norm.height * 0.5f);
                    Assert.IsTrue(u >= 0f && u <= 1f, $"UV.u={u} out of [0,1] for {file}[{i}]");
                    Assert.IsTrue(v >= 0f && v <= 1f, $"UV.v={v} out of [0,1] for {file}[{i}]");

                    // Pixels for pin storage
                    Rect pixelRect = BoundingBoxUtils.NormalizedToPixels(norm, 1280, 1024);
                    Assert.IsTrue(pixelRect.width > 0f, $"pixel rect width <= 0 for {file}[{i}]");
                    Assert.IsTrue(pixelRect.height > 0f, $"pixel rect height <= 0 for {file}[{i}]");

                    // Semantic label mapping
                    string semantic = SemanticLabelMapper.MapToMetaLabel(det.labels[i]);
                    Assert.IsNotNull(semantic, $"Null semantic label for {file}[{i}]");
                }
            }
        }
    }
}
