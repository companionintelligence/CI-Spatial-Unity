using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HouseMapper.Core;
using HouseMapper.Rendering;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HouseMapper.Mapping
{
    /// <summary>
    /// Unified CV pipeline: one frame → Florence2 → GroundingDINO → SAM2, all sequential.
    /// </summary>
    public class SpatialCapturePipeline : MonoBehaviour
    {
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
        [SerializeField] private string florenceDetectionTask = "<OD>";
        [SerializeField] private string florenceCaptionTask = "<MORE_DETAILED_CAPTION>";
        [SerializeField] private string groundingDinoFallbackPrompt = "furniture . object .";

        [Header("Pipeline Steps")]
        [SerializeField] private bool runFlorence = true;
        [SerializeField] private bool runGroundingDino = true;
        [SerializeField] private bool runSam2 = true;

        [Header("Optional Rendering")]
        [SerializeField] private BBoxRenderer bboxRenderer;
        [SerializeField] private MaskRenderer maskRenderer;
        [SerializeField] private CaptionPanel captionPanel;
        [SerializeField] private bool renderBoxes = true;
        [SerializeField] private bool renderMasks = true;

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

        public bool IsBusy => _isBusy;

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

        /// <summary>
        /// Capture a single frame and run the full pipeline sequentially:
        ///   1. Florence2 caption   → scene description
        ///   2. Florence2 OD        → labels + normalised bboxes
        ///   3. GroundingDINO       → refined bboxes + scores (prompt from Florence labels)
        ///   4. SAM2 segmentation   → masks for each detection
        /// </summary>
        public async void CaptureAndPin()
        {
            if (_isBusy || _cameraProvider == null || serverConfig == null)
            {
                return;
            }

            var frame = _cameraProvider.GetCurrentFrameJpeg(jpegQuality);
            if (frame == null || frame.Length == 0)
            {
                Debug.LogWarning("[Pipeline] Failed to capture frame from camera.");
                return;
            }

            _isBusy = true;
            Debug.Log($"[Pipeline] Captured frame ({frame.Length} bytes). Starting pipeline…");
            try
            {
                EnsureFrameTexture(frame);

                // ── Step 1: Florence2 caption ────────────────────────────
                string caption = null;
                if (runFlorence)
                {
                    Debug.Log($"[Pipeline] Step 1/4 – Florence2 caption ({florenceCaptionTask})…");
                    try
                    {
                        var captionResult = await _client.FlorenceAsync(frame, florenceCaptionTask);
                        caption = captionResult?.text;
                        LastCaption = caption;
                        Debug.Log($"[Pipeline] Florence2 caption: {caption}");
                        if (captionPanel != null && !string.IsNullOrEmpty(caption))
                        {
                            captionPanel.SetCaption(caption);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Pipeline] Florence2 caption failed: {ex.Message}");
                    }
                }

                // ── Step 2: Florence2 object detection ───────────────────
                List<string> florenceLabels = null;
                if (runFlorence)
                {
                    Debug.Log($"[Pipeline] Step 2/4 – Florence2 detection ({florenceDetectionTask})…");
                    try
                    {
                        var odResult = await _client.FlorenceAsync(frame, florenceDetectionTask);
                        florenceLabels = ExtractFlorenceLabels(odResult);
                        int boxCount = florenceLabels?.Count ?? 0;
                        Debug.Log($"[Pipeline] Florence2 detected {boxCount} objects: {string.Join(", ", florenceLabels ?? new List<string>())}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Pipeline] Florence2 OD failed: {ex.Message}");
                    }
                }

                // ── Step 3: GroundingDINO detection ──────────────────────
                DetectionResult detection = null;
                if (runGroundingDino)
                {
                    // Build a GroundingDINO prompt from Florence labels, or use fallback
                    string dinoPrompt = BuildDinoPrompt(florenceLabels);
                    Debug.Log($"[Pipeline] Step 3/4 – GroundingDINO detect (\"{dinoPrompt}\")…");
                    try
                    {
                        detection = await _client.DetectAsync(frame, dinoPrompt);
                        LastDetection = detection;
                        int detCount = detection?.labels?.Count ?? 0;
                        Debug.Log($"[Pipeline] GroundingDINO returned {detCount} detections.");
                        for (int i = 0; i < detCount; i++)
                        {
                            float s = i < detection.scores.Count ? detection.scores[i] : 0f;
                            Debug.Log($"  [{i}] {detection.labels[i]}  score={s:F3}  bbox=[{string.Join(",", detection.bboxes[i].Select(v => v.ToString("F3")))}]");
                        }

                        if (renderBoxes && bboxRenderer != null)
                        {
                            bboxRenderer.RenderBoxes(detection, _frameTexture);
                        }

                        AddPinsFromDetections(detection);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Pipeline] GroundingDINO failed: {ex.Message}");
                    }
                }

                // ── Step 4: SAM2 segmentation ────────────────────────────
                if (runSam2)
                {
                    // Build a SAM2 prompt from GroundingDINO labels, or Florence labels, or fallback
                    string samPrompt = BuildSamPrompt(detection, florenceLabels);
                    Debug.Log($"[Pipeline] Step 4/4 – SAM2 segment (\"{samPrompt}\")…");
                    try
                    {
                        var segmentation = await _client.SegmentAsync(frame, samPrompt);
                        LastSegmentation = segmentation;
                        int maskCount = segmentation?.annotations?.Count ?? 0;
                        Debug.Log($"[Pipeline] SAM2 returned {maskCount} masks.");

                        if (renderMasks && maskRenderer != null)
                        {
                            maskRenderer.RenderMasksAsync(segmentation);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Pipeline] SAM2 segmentation failed: {ex.Message}");
                    }
                }

                Debug.Log("[Pipeline] Pipeline complete.");
            }
            finally
            {
                _isBusy = false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

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

        private void AddPinsFromDetections(DetectionResult detection)
        {
            if (detection == null || detection.bboxes == null || raycaster == null || pinManager == null)
            {
                return;
            }

            int count = Mathf.Min(detection.bboxes.Count, detection.labels.Count);
            int added = 0;

            for (int i = 0; i < count; i++)
            {
                if (added >= maxPinsPerCapture)
                {
                    break;
                }

                float score = i < detection.scores.Count ? detection.scores[i] : 0f;
                if (score < minScore)
                {
                    continue;
                }

                var bbox = detection.bboxes[i];
                if (bbox == null || bbox.Length < 4)
                {
                    continue;
                }

                if (!BoundingBoxUtils.TryNormalize(bbox, _frameTexture.width, _frameTexture.height, out Rect normalized))
                {
                    continue;
                }

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
