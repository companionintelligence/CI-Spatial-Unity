using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompanionScan.Core;
using CompanionScan.Platform;
using CompanionScan.Rendering;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_ANDROID || UNITY_IOS
using UnityEngine.XR.ARFoundation;
#endif

namespace CompanionScan.Mapping
{
    /// <summary>
    /// Unified spatial capture orchestrator.  Two independent capture pipelines
    /// feed the same <see cref="SpatialPinManager"/>:
    /// <list type="number">
    ///   <item><b>CV Server pipeline</b> â€“ passthrough camera frame â†’ Florence2 â†’
    ///     GroundingDINO â†’ SAM2 â†’ raycasted <see cref="SpatialPin"/>s
    ///     (<see cref="PinSource.CvServer"/>).</item>
    ///   <item><b>Meta Scene Understanding</b> â€“ on-device OVRAnchor query via
    ///     <see cref="MetaSceneCollector"/> â†’ converted to
    ///     <see cref="SpatialPin"/>s (<see cref="PinSource.MetaScene"/>).</item>
    /// </list>
    /// Call <see cref="CaptureAndPin"/> for the CV pipeline,
    /// <see cref="CaptureMetaScene"/> for the Quest Scene API, or
    /// <see cref="CaptureAll"/> to run both back-to-back.
    /// </summary>
    public class SpatialCapturePipeline : MonoBehaviour
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Serialised fields â€” ordering preserved for scene compatibility
        //  (see [FormerlySerializedAs] on renamed fields)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("Server")]
        [SerializeField] private ServerConfig serverConfig;

        [Header("Camera")]
        [SerializeField] private MonoBehaviour cameraProviderSource;
        [SerializeField] private int jpegQuality = 75;
        [SerializeField] private bool preferArCameraOnQuest = true;

        [Header("Placement Quality")]
        [SerializeField] private bool enableTemporalSmoothing = true;
        [SerializeField, Range(0f, 1f)] private float smoothingAlpha = 0.5f;
        [SerializeField] private bool preferSemanticPlanes = true;
        [SerializeField] private float semanticPlaneMaxDistance = 6f;
        [SerializeField] private float trackDistanceThreshold = 0.4f;
        [SerializeField] private float trackMaxAgeSeconds = 2.5f;

        [Header("Mapping")]
        [SerializeField] private ViewportRaycaster raycaster;
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private SnapshotFrameStore snapshotStore;
        [SerializeField] private Camera snapshotCamera;
        [SerializeField] private float snapshotDistance = 1.0f;
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
        [SerializeField] private LabelRenderer labelRenderer;
        [SerializeField] private CaptionPanel captionPanel;
        [SerializeField] private bool renderBoxes = true;
        [FormerlySerializedAs("renderMasks")]
        [SerializeField] private bool renderMasks = true;

        [Header("Pipeline Steps â€” CV Server")]
        [FormerlySerializedAs("runCaption")]
        [SerializeField] private bool runFlorence = true;
        [FormerlySerializedAs("runSegmentation")]
        [SerializeField] private bool runSam2 = true;
        [SerializeField] private bool runGroundingDino = true;

        [Header("Pipeline Steps â€” Meta Scene Understanding")]
        [SerializeField] private MetaSceneCollector metaSceneCollector;
        [SerializeField] private bool runMetaSceneCapture = true;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Runtime state
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private ICameraProvider _cameraProvider;
        private CVServerClient _client;
        private bool _isBusy;
        private Texture2D _frameTexture;
        private CaptureFrame _lastCaptureFrame;
        private readonly Dictionary<string, Vector3> _smoothedPositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, List<TrackState>> _tracksByLabel = new Dictionary<string, List<TrackState>>();
        private string _captureSessionId;
        private int _captureFrameIndex;
        private int _nextTrackId = 1;
        private bool _lastCaptureHadIntrinsics;
        private int _lastFallbackHits;
        private bool _loggedMissingIntrinsics;

        private class TrackState
        {
            public int id;
            public Vector3 position;
            public double timestamp;
        }

        /// <summary>Last caption produced by Florence2.</summary>
        public string LastCaption { get; private set; }

        /// <summary>Last GroundingDINO detection result.</summary>
        public DetectionResult LastDetection { get; private set; }

        /// <summary>Last SAM2 segmentation result.</summary>
        public SegmentationResult LastSegmentation { get; private set; }

        /// <summary>Last Meta Scene Understanding snapshot.</summary>
        public MetaSceneSnapshot LastMetaSnapshot { get; private set; }

        public bool LastCaptureHadIntrinsics => _lastCaptureHadIntrinsics;
        public int LastFallbackHitCount => _lastFallbackHits;
        public ServerConfig ServerConfig => serverConfig;

        public bool IsBusy => _isBusy;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Runtime toggle properties (driven by ControllerMenuUI)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Enable / disable the Florence2 caption step at runtime.</summary>
        public bool RunFlorence { get => runFlorence; set => runFlorence = value; }

        /// <summary>Enable / disable the GroundingDINO detection step at runtime.</summary>
        public bool RunGroundingDino { get => runGroundingDino; set => runGroundingDino = value; }

        /// <summary>Enable / disable the SAM2 segmentation step at runtime.</summary>
        public bool RunSam2 { get => runSam2; set => runSam2 = value; }

        /// <summary>Enable / disable Meta Scene Understanding capture at runtime.</summary>
        public bool RunMetaSceneCapture { get => runMetaSceneCapture; set => runMetaSceneCapture = value; }

        /// <summary>Enable / disable 2-D bounding-box rendering at runtime.</summary>
        public bool RenderBoxes { get => renderBoxes; set => renderBoxes = value; }

        /// <summary>Enable / disable mask overlay rendering at runtime.</summary>
        public bool RenderMasks { get => renderMasks; set => renderMasks = value; }

        /// <summary>The viewport raycaster used for 3D pin placement.
        /// Exposed so the UI can update <see cref="ViewportRaycaster.FallbackDistance"/>.</summary>
        public ViewportRaycaster Raycaster => raycaster;

        /// <summary>The spatial pin manager â€” exposed so UI can toggle pin visibility.</summary>
        public SpatialPinManager PinManager => pinManager;

        /// <summary>The label renderer â€” exposed so UI can toggle 3D label visibility.</summary>
        public LabelRenderer LabelRenderer => labelRenderer;

        /// <summary>Distance used when capturing a snapshot frustum frame.</summary>
        public float SnapshotDistance
        {
            get => snapshotDistance;
            set => snapshotDistance = Mathf.Max(0f, value);
        }

        /// <summary>Camera used for snapshot frustum calculations.</summary>
        public Camera SnapshotCamera => snapshotCamera != null ? snapshotCamera : Camera.main;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Lifecycle
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            _client = new CVServerClient(serverConfig);
            _captureSessionId = Guid.NewGuid().ToString("N");
#if UNITY_ANDROID
            if (preferArCameraOnQuest)
            {
                TryAutoSetupArCameraProvider();
            }
#endif
            _cameraProvider = cameraProviderSource as ICameraProvider;
        }

#if UNITY_ANDROID
        private void TryAutoSetupArCameraProvider()
        {
            if (cameraProviderSource is MobileCameraProvider)
            {
                return;
            }

            var existingProvider = FindAnyObjectByType<MobileCameraProvider>();
            if (existingProvider == null)
            {
                var session = FindAnyObjectByType<ARSession>();
                if (session == null)
                {
                    var sessionGo = new GameObject("ARSession");
                    session = sessionGo.AddComponent<ARSession>();
                }

                var cameraGo = new GameObject("PcaCamera");
                cameraGo.transform.SetParent(transform, false);
                var camera = cameraGo.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.Nothing;
                camera.cullingMask = 0;
                cameraGo.AddComponent<ARCameraManager>();
                cameraGo.AddComponent<ARCameraBackground>();
                existingProvider = cameraGo.AddComponent<MobileCameraProvider>();
                Debug.Log("[Pipeline] Created ARSession + PCA camera for passthrough capture.");
            }

            cameraProviderSource = existingProvider;
        }
#endif

        private void OnEnable()
        {
            _cameraProvider?.StartCapture();
        }

        private void OnDisable()
        {
            _cameraProvider?.StopCapture();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Public API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
                Debug.Log("[Pipeline] â•â•â• CaptureAll: running both pipelines â•â•â•");

                // Meta Scene (fast, local, no network)
                if (runMetaSceneCapture)
                {
                    await RunMetaSceneCaptureAsync();
                }

                // CV Server (network)
                await RunCvPipelineAsync();

                Debug.Log("[Pipeline] â•â•â• CaptureAll complete â•â•â•");
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>
        /// Run only the CV Server pipeline: Florence2 â†’ GroundingDINO â†’ SAM2.
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Pipeline A â€” Meta Scene Understanding
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private async Task RunMetaSceneCaptureAsync()
        {
            if (metaSceneCollector == null)
            {
                Debug.LogWarning("[Pipeline-Meta] MetaSceneCollector is not assigned.");
                return;
            }

            Debug.Log("[Pipeline-Meta] Querying Meta Scene Understandingâ€¦");
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Pipeline B â€” CV Server (Florence2 â†’ GroundingDINO â†’ SAM2)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

            Debug.Log($"[Pipeline-CV] Captured frame ({frame.Length} bytes). Starting CV pipelineâ€¦");
            EnsureFrameTexture(frame);
            var capture = BuildCaptureFrame(frame);
            _lastCaptureHadIntrinsics = capture.HasIntrinsics;
            _lastFallbackHits = 0;
            if (!_lastCaptureHadIntrinsics && !_loggedMissingIntrinsics)
            {
                _loggedMissingIntrinsics = true;
                Debug.LogWarning("[Pipeline-CV] Camera intrinsics unavailable; using pose-only rays.");
            }
            _lastCaptureFrame = capture;
            if (bboxRenderer != null)
            {
                bboxRenderer.SetPreviewTexture(_frameTexture);
            }
            CaptureSnapshotFrame(capture);
            _captureFrameIndex++;

            // â”€â”€ Step 1: Florence2 caption â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            string caption = null;
            if (runFlorence)
            {
                Debug.Log($"[Pipeline-CV] Step 1/4 â€“ Florence2 caption ({florenceCaptionTask})â€¦");
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

            // â”€â”€ Step 2: Florence2 object detection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            List<string> florenceLabels = null;
            if (runFlorence)
            {
                Debug.Log($"[Pipeline-CV] Step 2/4 â€“ Florence2 detection ({florenceDetectionTask})â€¦");
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

            // â”€â”€ Step 3: GroundingDINO detection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            DetectionResult detection = null;
            if (runGroundingDino)
            {
                string dinoPrompt = BuildDinoPrompt(florenceLabels);
                Debug.Log($"[Pipeline-CV] Step 3/4 â€“ GroundingDINO detect (\"{dinoPrompt}\")â€¦");
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

                    AddCvPinsFromDetections(detection, capture);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Pipeline-CV] GroundingDINO failed: {ex.Message}");
                }
            }

            // â”€â”€ Step 4: SAM2 segmentation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (runSam2)
            {
                string samPrompt = BuildSamPrompt(detection, florenceLabels);
                Debug.Log($"[Pipeline-CV] Step 4/4 â€“ SAM2 segment (\"{samPrompt}\")â€¦");
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

                    // When GDino step was skipped, create spatial pins from SAM2
                    // annotations (which include GDino results from the server).
                    if (!runGroundingDino && segmentation?.annotations != null)
                    {
                        AddCvPinsFromSegmentation(segmentation, capture);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Pipeline-CV] SAM2 segmentation failed: {ex.Message}");
                }
            }

            // â”€â”€ 3D label rendering â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (labelRenderer != null && pinManager != null)
            {
                var positions = new List<Vector3>();
                var labels = new List<string>();
                foreach (var pin in pinManager.CvPins)
                {
                    positions.Add(pin.position);
                    labels.Add(pin.label);
                }
                if (positions.Count > 0)
                {
                    labelRenderer.RenderLabels(positions, labels);
                    Debug.Log($"[Pipeline-CV] Rendered {positions.Count} 3D labels.");
                }
            }

            Debug.Log("[Pipeline-CV] CV pipeline complete.");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Helpers
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

        private CaptureFrame BuildCaptureFrame(byte[] jpegBytes)
        {
            var cam = snapshotCamera != null ? snapshotCamera : Camera.main;
            Pose pose = cam != null
                ? new Pose(cam.transform.position, cam.transform.rotation)
                : new Pose(Vector3.zero, Quaternion.identity);

            if (_cameraProvider != null)
            {
                pose = _cameraProvider.GetCameraPose();
            }

            var intrinsics = _cameraProvider != null
                ? _cameraProvider.GetCameraIntrinsics()
                : Matrix4x4.identity;

            int width = _frameTexture != null ? _frameTexture.width : 0;
            int height = _frameTexture != null ? _frameTexture.height : 0;

            var capture = CaptureFrame.Create(jpegBytes, width, height, pose, intrinsics);
            capture.sessionId = _captureSessionId;
            capture.frameIndex = _captureFrameIndex;
            capture.id = $"{_captureSessionId}_{_captureFrameIndex:0000}";
            return capture;
        }

        private void CaptureSnapshotFrame(CaptureFrame capture)
        {
            if (snapshotStore == null)
            {
                return;
            }

            var cam = snapshotCamera != null ? snapshotCamera : Camera.main;
            capture.id = snapshotStore.AddSnapshot(capture, snapshotDistance, cam);
        }

        /// <summary>
        /// Create spatial pins from SAM2 segment annotations (used when GDino
        /// detection step is skipped but /v1/segment returns GDino results).
        /// </summary>
        private void AddCvPinsFromSegmentation(SegmentationResult segmentation, CaptureFrame capture)
        {
            if (segmentation?.annotations == null || raycaster == null || pinManager == null)
                return;

            int imgW = capture != null ? capture.imageWidth : (_frameTexture != null ? _frameTexture.width : 0);
            int imgH = capture != null ? capture.imageHeight : (_frameTexture != null ? _frameTexture.height : 0);
            int added = 0;

            foreach (var ann in segmentation.annotations)
            {
                if (added >= maxPinsPerCapture) break;
                if (ann.score < minScore) continue;
                if (ann.bbox == null || ann.bbox.Length < 4) continue;

                if (!BoundingBoxUtils.TryNormalize(ann.bbox, imgW, imgH, out Rect normalized))
                    continue;

                float u = normalized.x + normalized.width * 0.5f;
                float v = 1.0f - (normalized.y + normalized.height * 0.5f);
                Rect pixelRect = BoundingBoxUtils.NormalizedToPixels(normalized, imgW, imgH);

                var pixel = new Vector2(pixelRect.x + pixelRect.width * 0.5f,
                                        pixelRect.y + pixelRect.height * 0.5f);

                if (!TryResolveHit(capture, new Vector2(u, v), pixel, out Vector3 hitPoint, out Vector3 hitNormal, out RaycastHitType hitType))
                    hitNormal = Vector3.up;

                string semantic = SemanticLabelMapper.MapToMetaLabel(ann.label);
                var position = ApplySmoothing(semantic, hitPoint);
                float confidence = ComputePlacementConfidence(ann.score, hitType);

                int trackId = AssignTrackId(semantic, position, capture?.timestamp ?? Time.timeAsDouble);
                var pin = new SpatialPin
                {
                    label = ann.label,
                    semanticLabel = semantic,
                    score = ann.score,
                    source = PinSource.CvServer,
                    position = position,
                    normal = hitNormal,
                    bboxPixels = new SerializableRect(pixelRect.x, pixelRect.y, pixelRect.width, pixelRect.height),
                    bboxNormalized = new SerializableRect(normalized.x, normalized.y, normalized.width, normalized.height),
                    sourcePrompt = "segment",
                    timestamp = capture != null ? capture.timestamp : Time.timeAsDouble,
                    captureId = capture?.id,
                    captureSessionId = capture?.sessionId,
                    captureFrameIndex = capture?.frameIndex ?? 0,
                    placementConfidence = confidence,
                    hitType = hitType,
                    trackId = trackId
                };

                pinManager.AddPin(pin);
                added++;
            }

            Debug.Log($"[Pipeline-CV] Created {added} spatial pins from SAM2 annotations.");
        }

        private void AddCvPinsFromDetections(DetectionResult detection, CaptureFrame capture)
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

                int imgW = capture != null ? capture.imageWidth : (_frameTexture != null ? _frameTexture.width : 0);
                int imgH = capture != null ? capture.imageHeight : (_frameTexture != null ? _frameTexture.height : 0);
                if (!BoundingBoxUtils.TryNormalize(bbox, imgW, imgH, out Rect normalized))
                    continue;

                float u = normalized.x + normalized.width * 0.5f;
                float v = 1.0f - (normalized.y + normalized.height * 0.5f);
                Rect pixelRect = BoundingBoxUtils.NormalizedToPixels(normalized, imgW, imgH);

                var pixel = new Vector2(pixelRect.x + pixelRect.width * 0.5f,
                                        pixelRect.y + pixelRect.height * 0.5f);

                if (!TryResolveHit(capture, new Vector2(u, v), pixel, out Vector3 hitPoint, out Vector3 hitNormal, out RaycastHitType hitType))
                {
                    hitNormal = Vector3.up;
                }

                string semantic = SemanticLabelMapper.MapToMetaLabel(detection.labels[i]);
                var position = ApplySmoothing(semantic, hitPoint);
                float confidence = ComputePlacementConfidence(score, hitType);

                int trackId = AssignTrackId(semantic, position, capture?.timestamp ?? Time.timeAsDouble);
                var pin = new SpatialPin
                {
                    label = detection.labels[i],
                    semanticLabel = semantic,
                    score = score,
                    source = PinSource.CvServer,
                    position = position,
                    normal = hitNormal,
                    bboxPixels = new SerializableRect(pixelRect.x, pixelRect.y, pixelRect.width, pixelRect.height),
                    bboxNormalized = new SerializableRect(normalized.x, normalized.y, normalized.width, normalized.height),
                    sourcePrompt = BuildDinoPrompt(null),
                    timestamp = capture != null ? capture.timestamp : Time.timeAsDouble,
                    captureId = capture?.id,
                    captureSessionId = capture?.sessionId,
                    captureFrameIndex = capture?.frameIndex ?? 0,
                    placementConfidence = confidence,
                    hitType = hitType,
                    trackId = trackId
                };

                pinManager.AddPin(pin);
                added++;
            }
        }

        private bool TryResolveHit(CaptureFrame capture, Vector2 uv, Vector2 pixel,
                                   out Vector3 hitPoint, out Vector3 hitNormal, out RaycastHitType hitType)
        {
            if (raycaster == null)
            {
                hitPoint = Vector3.zero;
                hitNormal = Vector3.up;
                hitType = RaycastHitType.Fallback;
                return false;
            }

            if (capture != null && capture.HasIntrinsics && capture.imageWidth > 0 && capture.imageHeight > 0)
            {
                bool hit = raycaster.TryRaycastCapture(capture, pixel, false, out hitPoint, out hitNormal, out hitType);
                if (hit)
                {
                    return true;
                }

                if (preferSemanticPlanes && TryProjectToSemanticPlane(capture, pixel, out hitPoint, out hitNormal))
                {
                    hitType = RaycastHitType.MetaPlane;
                    return true;
                }

                bool fallbackHit = raycaster.TryRaycastCapture(capture, pixel, true, out hitPoint, out hitNormal, out hitType);
                if (hitType == RaycastHitType.Fallback)
                {
                    _lastFallbackHits++;
                }
                return fallbackHit;
            }

            bool viewportHit = raycaster.TryRaycastViewport(uv, out hitPoint, out hitNormal, out hitType);
            if (viewportHit)
            {
                return true;
            }

            if (preferSemanticPlanes && TryProjectToSemanticPlaneFromViewport(uv, out hitPoint, out hitNormal))
            {
                hitType = RaycastHitType.MetaPlane;
                return true;
            }

            bool viewportFallback = raycaster.TryRaycastViewport(uv, out hitPoint, out hitNormal, out hitType);
            if (hitType == RaycastHitType.Fallback)
            {
                _lastFallbackHits++;
            }
            return viewportFallback;
        }

        private Vector3 ApplySmoothing(string semanticLabel, Vector3 position)
        {
            if (!enableTemporalSmoothing || string.IsNullOrWhiteSpace(semanticLabel))
            {
                return position;
            }

            if (_smoothedPositions.TryGetValue(semanticLabel, out var previous))
            {
                var smoothed = Vector3.Lerp(previous, position, smoothingAlpha);
                _smoothedPositions[semanticLabel] = smoothed;
                return smoothed;
            }

            _smoothedPositions[semanticLabel] = position;
            return position;
        }

        private static float ComputePlacementConfidence(float modelScore, RaycastHitType hitType)
        {
            float weight = hitType switch
            {
                RaycastHitType.ArPlane => 1f,
                RaycastHitType.Environment => 0.9f,
                RaycastHitType.MetaPlane => 0.85f,
                RaycastHitType.Physics => 0.7f,
                _ => 0.4f
            };

            return Mathf.Clamp01(modelScore * weight);
        }

        private int AssignTrackId(string semanticLabel, Vector3 position, double timestamp)
        {
            if (string.IsNullOrWhiteSpace(semanticLabel))
            {
                return 0;
            }

            if (!_tracksByLabel.TryGetValue(semanticLabel, out var tracks))
            {
                tracks = new List<TrackState>();
                _tracksByLabel[semanticLabel] = tracks;
            }

            tracks.RemoveAll(t => (timestamp - t.timestamp) > trackMaxAgeSeconds);

            TrackState best = null;
            float bestDist = float.MaxValue;
            foreach (var track in tracks)
            {
                float dist = Vector3.Distance(track.position, position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = track;
                }
            }

            if (best != null && bestDist <= trackDistanceThreshold)
            {
                best.position = position;
                best.timestamp = timestamp;
                return best.id;
            }

            var created = new TrackState
            {
                id = _nextTrackId++,
                position = position,
                timestamp = timestamp
            };
            tracks.Add(created);
            return created.id;
        }

        private bool TryProjectToSemanticPlane(CaptureFrame capture, Vector2 pixel, out Vector3 point, out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.up;

            if (metaSceneCollector == null || metaSceneCollector.LastSnapshot == null)
            {
                return false;
            }

            var ray = ViewportRaycaster.BuildRayFromIntrinsics(capture, pixel);
            return TryProjectToSemanticPlane(ray, out point, out normal);
        }

        private bool TryProjectToSemanticPlaneFromViewport(Vector2 uv, out Vector3 point, out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.up;

            var cam = snapshotCamera != null ? snapshotCamera : Camera.main;
            if (cam == null)
            {
                return false;
            }

            var ray = cam.ViewportPointToRay(new Vector3(uv.x, uv.y, 0f));
            return TryProjectToSemanticPlane(ray, out point, out normal);
        }

        private bool TryProjectToSemanticPlane(Ray ray, out Vector3 point, out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.up;

            if (!preferSemanticPlanes || metaSceneCollector == null || metaSceneCollector.LastSnapshot == null)
            {
                return false;
            }

            float bestT = float.MaxValue;
            MetaSceneAnchorSnapshot bestAnchor = null;

            foreach (var anchor in metaSceneCollector.LastSnapshot.anchors)
            {
                if (!anchor.hasPlane)
                {
                    continue;
                }

                if (!IsPreferredPlaneLabel(anchor.labels))
                {
                    continue;
                }

                var planeNormal = anchor.rotation * Vector3.up;
                float denom = Vector3.Dot(planeNormal, ray.direction);
                if (Mathf.Abs(denom) < 0.0001f)
                {
                    continue;
                }

                float t = Vector3.Dot(anchor.position - ray.origin, planeNormal) / denom;
                if (t <= 0f || t > semanticPlaneMaxDistance || t >= bestT)
                {
                    continue;
                }

                var hit = ray.origin + ray.direction * t;
                if (!IsPointOnPlane(anchor, hit))
                {
                    continue;
                }

                bestT = t;
                bestAnchor = anchor;
            }

            if (bestAnchor == null)
            {
                return false;
            }

            normal = bestAnchor.rotation * Vector3.up;
            point = ray.origin + ray.direction * bestT;
            return true;
        }

        private static bool IsPreferredPlaneLabel(List<string> labels)
        {
            if (labels == null || labels.Count == 0)
            {
                return false;
            }

            foreach (var label in labels)
            {
                switch (label)
                {
                    case "FLOOR":
                    case "TABLE":
                    case "WALL_FACE":
                        return true;
                }
            }

            return false;
        }

        private static bool IsPointOnPlane(MetaSceneAnchorSnapshot anchor, Vector3 point)
        {
            var local = Quaternion.Inverse(anchor.rotation) * (point - anchor.position);
            if (anchor.planeBoundary != null && anchor.planeBoundary.Count >= 3)
            {
                var polygon = new List<Vector2>(anchor.planeBoundary.Count);
                for (int i = 0; i < anchor.planeBoundary.Count; i++)
                {
                    var localPoint = Quaternion.Inverse(anchor.rotation) * (anchor.planeBoundary[i] - anchor.position);
                    polygon.Add(new Vector2(localPoint.x, localPoint.y));
                }

                return PointInPolygon(new Vector2(local.x, local.y), polygon);
            }

            float halfX = anchor.planeSize.x * 0.5f;
            float halfY = anchor.planeSize.y * 0.5f;
            return Mathf.Abs(local.x) <= halfX && Mathf.Abs(local.y) <= halfY;
        }

        private static bool PointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                bool intersect = ((pi.y > point.y) != (pj.y > point.y)) &&
                                 (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y + 0.0001f) + pi.x);
                if (intersect)
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}

