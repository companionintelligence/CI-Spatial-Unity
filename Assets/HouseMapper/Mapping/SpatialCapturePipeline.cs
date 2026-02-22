using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HouseMapper.Core;
using HouseMapper.Rendering;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace HouseMapper.Mapping
{
    /// <summary>
    /// Unified spatial capture orchestrator.  Two independent capture pipelines
    /// feed the same <see cref="SpatialPinManager"/>:
    /// <list type="number">
    ///   <item><b>CV Server pipeline</b> – passthrough camera frame → Florence2 →
    ///     GroundingDINO → SAM2 → raycasted <see cref="SpatialPin"/>s
    ///     (<see cref="PinSource.CvServer"/>).</item>
    ///   <item><b>Meta Scene Understanding</b> – on-device OVRAnchor query via
    ///     <see cref="MetaSceneCollector"/> → converted to
    ///     <see cref="SpatialPin"/>s (<see cref="PinSource.MetaScene"/>).</item>
    /// </list>
    /// Call <see cref="CaptureAndPin"/> for the CV pipeline,
    /// <see cref="CaptureMetaScene"/> for the Quest Scene API, or
    /// <see cref="CaptureAll"/> to run both back-to-back.
    /// </summary>
    public class SpatialCapturePipeline : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════
        //  Serialised fields — ordering preserved for scene compatibility
        //  (see [FormerlySerializedAs] on renamed fields)
        // ═══════════════════════════════════════════════════════════════

        [Header("Server")]
        [SerializeField] private ServerConfig serverConfig;

        [Header("Camera")]
        [SerializeField] private MonoBehaviour cameraProviderSource;
        [SerializeField] private int jpegQuality = 75;

        [Header("Mapping")]
        [SerializeField] private ViewportRaycaster raycaster;
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private float minScore = 0.3f;
        [SerializeField] private int maxPinsPerCapture = 20;

        [Header("Prompts")]
        [FormerlySerializedAs("detectionPrompt")]
        [SerializeField] private string groundingDinoFallbackPrompt = "furniture . object .";
        [FormerlySerializedAs("segmentationPrompt")]
        [SerializeField] private string florenceDetectionTask = "<OD>";
        [FormerlySerializedAs("captionTask")]
        [SerializeField] private string florenceCaptionTask = "<MORE_DETAILED_CAPTION>";

        [Header("Optional Rendering")]
        [SerializeField] private BBoxRenderer bboxRenderer;
        [SerializeField] private MaskRenderer maskRenderer;
        [SerializeField] private CaptionPanel captionPanel;
        [SerializeField] private bool renderBoxes = true;
        [FormerlySerializedAs("renderMasks")]
        [SerializeField] private bool renderMasks = true;

        [Header("Pipeline Steps — CV Server")]
        [FormerlySerializedAs("runCaption")]
        [SerializeField] private bool runFlorence = true;
        [FormerlySerializedAs("runSegmentation")]
        [SerializeField] private bool runSam2 = true;
        [SerializeField] private bool runGroundingDino = true;

        [Header("Pipeline Steps — Meta Scene Understanding")]
        [SerializeField] private MetaSceneCollector metaSceneCollector;
        [SerializeField] private bool runMetaSceneCapture = true;

        // ═══════════════════════════════════════════════════════════════
        //  Runtime state
        // ═══════════════════════════════════════════════════════════════

        private ICameraProvider _cameraProvider;
        private CVServerClient _client;
        private bool _isBusy;
        private Texture2D _frameTexture;

        /// <summary>Last caption produced by Florence2.</summary>
        public string LastCaption { get; private set; }

        /// <summary>Last GroundingDINO detection result.</summary>
        public DetectionResult LastDetection { get; private set; }

        /// <summary>Last SAM2 segmentation result.</summary>
        public SegmentationResult LastSegmentation { get; private set; }

        /// <summary>Last Meta Scene Understanding snapshot.</summary>
        public MetaSceneSnapshot LastMetaSnapshot { get; private set; }

        public bool IsBusy => _isBusy;

        // ═══════════════════════════════════════════════════════════════
        //  Lifecycle
        // ═══════════════════════════════════════════════════════════════

        private void Awake()
        {
            _cameraProvider = cameraProviderSource as ICameraProvider;
            _client = new CVServerClient(serverConfig);
        }

        private void OnEnable()
        {
            _cameraProvider?.StartCapture();
        }

        private void OnDisable()
        {
            _cameraProvider?.StopCapture();
        }

        // ═══════════════════════════════════════════════════════════════
        //  Public API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Run <b>both</b> capture pipelines back-to-back:
        ///   1. Meta Scene Understanding (on-device, fast)
        ///   2. CV Server pipeline (network, slower)
        /// Pins from both systems are added to the shared
        /// <see cref="SpatialPinManager"/>.
        /// </summary>
        public async void CaptureAll()
        {
            if (_isBusy) return;

            _isBusy = true;
            try
            {
                Debug.Log("[Pipeline] ═══ CaptureAll: running both pipelines ═══");

                // Meta Scene (fast, local, no network)
                if (runMetaSceneCapture)
                {
                    await RunMetaSceneCaptureAsync();
                }

                // CV Server (network)
                await RunCvPipelineAsync();

                Debug.Log("[Pipeline] ═══ CaptureAll complete ═══");
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>
        /// Run only the CV Server pipeline: Florence2 → GroundingDINO → SAM2.
        /// Pins are tagged <see cref="PinSource.CvServer"/>.
        /// </summary>
        public async void CaptureAndPin()
        {
            if (_isBusy || _cameraProvider == null || serverConfig == null) return;

            _isBusy = true;
            try
            {
                await RunCvPipelineAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>
        /// Run only Meta Scene Understanding capture.
        /// Pins are tagged <see cref="PinSource.MetaScene"/>.
        /// </summary>
        public async void CaptureMetaScene()
        {
            if (_isBusy) return;

            _isBusy = true;
            try
            {
                await RunMetaSceneCaptureAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Pipeline A — Meta Scene Understanding
        // ═══════════════════════════════════════════════════════════════

        private async Task RunMetaSceneCaptureAsync()
        {
            if (metaSceneCollector == null)
            {
                Debug.LogWarning("[Pipeline-Meta] MetaSceneCollector is not assigned.");
                return;
            }

            Debug.Log("[Pipeline-Meta] Querying Meta Scene Understanding…");
            var snapshot = await metaSceneCollector.CaptureSnapshotAsync();
            LastMetaSnapshot = snapshot;

            if (!string.IsNullOrEmpty(snapshot.error))
            {
                Debug.LogWarning($"[Pipeline-Meta] {snapshot.error}");
                return;
            }

            Debug.Log($"[Pipeline-Meta] Captured {snapshot.anchors.Count} scene anchors.");

            // Convert to SpatialPins and add to the shared pin manager
            var pins = metaSceneCollector.ConvertSnapshotToPins(snapshot);
            if (pinManager != null)
            {
                int added = 0;
                foreach (var pin in pins)
                {
                    pinManager.AddPin(pin);
                    added++;
                }
                Debug.Log($"[Pipeline-Meta] Added {added} Meta Scene pins to pin manager.");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Pipeline B — CV Server (Florence2 → GroundingDINO → SAM2)
        // ═══════════════════════════════════════════════════════════════

        private async Task RunCvPipelineAsync()
        {
            if (_cameraProvider == null || serverConfig == null)
            {
                Debug.LogWarning("[Pipeline-CV] Camera provider or server config is not assigned.");
                return;
            }

            var frame = _cameraProvider.GetCurrentFrameJpeg(jpegQuality);
            if (frame == null || frame.Length == 0)
            {
                Debug.LogWarning("[Pipeline-CV] Failed to capture frame from camera.");
                return;
            }

            Debug.Log($"[Pipeline-CV] Captured frame ({frame.Length} bytes). Starting CV pipeline…");
            EnsureFrameTexture(frame);

            // ── Step 1: Florence2 caption ────────────────────────────
            string caption = null;
            if (runFlorence)
            {
                Debug.Log($"[Pipeline-CV] Step 1/4 – Florence2 caption ({florenceCaptionTask})…");
                try
                {
                    var captionResult = await _client.FlorenceAsync(frame, florenceCaptionTask);
                    caption = captionResult?.text;
                    LastCaption = caption;
                    Debug.Log($"[Pipeline-CV] Florence2 caption: {caption}");
                    if (captionPanel != null && !string.IsNullOrEmpty(caption))
                    {
                        captionPanel.SetCaption(caption);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Pipeline-CV] Florence2 caption failed: {ex.Message}");
                }
            }

            // ── Step 2: Florence2 object detection ───────────────────
            List<string> florenceLabels = null;
            if (runFlorence)
            {
                Debug.Log($"[Pipeline-CV] Step 2/4 – Florence2 detection ({florenceDetectionTask})…");
                try
                {
                    var odResult = await _client.FlorenceAsync(frame, florenceDetectionTask);
                    florenceLabels = ExtractFlorenceLabels(odResult);
                    int boxCount = florenceLabels?.Count ?? 0;
                    Debug.Log($"[Pipeline-CV] Florence2 detected {boxCount} objects: " +
                              $"{string.Join(", ", florenceLabels ?? new List<string>())}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Pipeline-CV] Florence2 OD failed: {ex.Message}");
                }
            }

            // ── Step 3: GroundingDINO detection ──────────────────────
            DetectionResult detection = null;
            if (runGroundingDino)
            {
                string dinoPrompt = BuildDinoPrompt(florenceLabels);
                Debug.Log($"[Pipeline-CV] Step 3/4 – GroundingDINO detect (\"{dinoPrompt}\")…");
                try
                {
                    detection = await _client.DetectAsync(frame, dinoPrompt);
                    LastDetection = detection;
                    int detCount = detection?.labels?.Count ?? 0;
                    Debug.Log($"[Pipeline-CV] GroundingDINO returned {detCount} detections.");
                    for (int i = 0; i < detCount; i++)
                    {
                        float s = i < detection.scores.Count ? detection.scores[i] : 0f;
                        Debug.Log($"  [{i}] {detection.labels[i]}  score={s:F3}  " +
                                  $"bbox=[{string.Join(",", detection.bboxes[i].Select(v => v.ToString("F3")))}]");
                    }

                    if (renderBoxes && bboxRenderer != null)
                    {
                        bboxRenderer.RenderBoxes(detection, _frameTexture);
                    }

                    AddCvPinsFromDetections(detection);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Pipeline-CV] GroundingDINO failed: {ex.Message}");
                }
            }

            // ── Step 4: SAM2 segmentation ────────────────────────────
            if (runSam2)
            {
                string samPrompt = BuildSamPrompt(detection, florenceLabels);
                Debug.Log($"[Pipeline-CV] Step 4/4 – SAM2 segment (\"{samPrompt}\")…");
                try
                {
                    var segmentation = await _client.SegmentAsync(frame, samPrompt);
                    LastSegmentation = segmentation;
                    int maskCount = segmentation?.annotations?.Count ?? 0;
                    Debug.Log($"[Pipeline-CV] SAM2 returned {maskCount} masks.");

                    if (renderMasks && maskRenderer != null)
                    {
                        maskRenderer.RenderMasksAsync(segmentation);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Pipeline-CV] SAM2 segmentation failed: {ex.Message}");
                }
            }

            Debug.Log("[Pipeline-CV] CV pipeline complete.");
        }

        // ═══════════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Extract unique labels from a Florence2 OD response.
        /// The server returns results.labels as a JSON array.
        /// </summary>
        private static List<string> ExtractFlorenceLabels(FlorenceResult result)
        {
            if (result?.results == null) return null;

            var labelsToken = result.results["labels"] as JArray;
            if (labelsToken == null) return null;

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in labelsToken)
            {
                var label = t?.ToString();
                if (!string.IsNullOrWhiteSpace(label))
                    unique.Add(label.Trim());
            }
            return unique.Count > 0 ? unique.ToList() : null;
        }

        /// <summary>
        /// Build a GroundingDINO-style prompt ("label1 . label2 .") from Florence labels.
        /// </summary>
        private string BuildDinoPrompt(List<string> florenceLabels)
        {
            if (florenceLabels != null && florenceLabels.Count > 0)
            {
                return string.Join(" . ", florenceLabels) + " .";
            }
            return groundingDinoFallbackPrompt;
        }

        /// <summary>
        /// Build a SAM2 prompt from the best available labels.
        /// </summary>
        private string BuildSamPrompt(DetectionResult detection, List<string> florenceLabels)
        {
            // Prefer GroundingDINO labels (filtered by score)
            if (detection?.labels != null && detection.labels.Count > 0)
            {
                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < detection.labels.Count; i++)
                {
                    float s = i < detection.scores.Count ? detection.scores[i] : 0f;
                    if (s >= minScore)
                        unique.Add(detection.labels[i].Trim());
                }
                if (unique.Count > 0)
                    return string.Join(" . ", unique) + " .";
            }

            // Fall back to Florence labels
            if (florenceLabels != null && florenceLabels.Count > 0)
            {
                return string.Join(" . ", florenceLabels) + " .";
            }

            return groundingDinoFallbackPrompt;
        }

        private void EnsureFrameTexture(byte[] jpegBytes)
        {
            if (_frameTexture == null)
            {
                _frameTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            }
            _frameTexture.LoadImage(jpegBytes);
        }

        private void AddCvPinsFromDetections(DetectionResult detection)
        {
            if (detection == null || detection.bboxes == null || raycaster == null || pinManager == null)
            {
                return;
            }

            int count = Mathf.Min(detection.bboxes.Count, detection.labels.Count);
            int added = 0;

            for (int i = 0; i < count; i++)
            {
                if (added >= maxPinsPerCapture) break;

                float score = i < detection.scores.Count ? detection.scores[i] : 0f;
                if (score < minScore) continue;

                var bbox = detection.bboxes[i];
                if (bbox == null || bbox.Length < 4) continue;

                if (!BoundingBoxUtils.TryNormalize(bbox, _frameTexture.width, _frameTexture.height, out Rect normalized))
                    continue;

                float u = normalized.x + normalized.width * 0.5f;
                float v = 1.0f - (normalized.y + normalized.height * 0.5f);
                Rect pixelRect = BoundingBoxUtils.NormalizedToPixels(normalized, _frameTexture.width, _frameTexture.height);

                if (!raycaster.TryRaycastViewport(new Vector2(u, v), out Vector3 hitPoint, out Vector3 hitNormal))
                {
                    hitNormal = Vector3.up;
                }

                var pin = new SpatialPin
                {
                    label = detection.labels[i],
                    semanticLabel = SemanticLabelMapper.MapToMetaLabel(detection.labels[i]),
                    score = score,
                    source = PinSource.CvServer,
                    position = hitPoint,
                    normal = hitNormal,
                    bboxPixels = new SerializableRect(pixelRect.x, pixelRect.y, pixelRect.width, pixelRect.height),
                    bboxNormalized = new SerializableRect(normalized.x, normalized.y, normalized.width, normalized.height),
                    sourcePrompt = BuildDinoPrompt(null),
                    timestamp = Time.timeAsDouble
                };

                pinManager.AddPin(pin);
                added++;
            }
        }
    }
}
