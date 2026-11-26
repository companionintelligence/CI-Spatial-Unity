using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Companion.Spatial;
using Companion.Spatial.Tracking;
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
        [Tooltip("Seconds without a detection before a track is called lost. A lost track is not forgotten: " +
                 "the same object seen again later is still that track, so one pin.")]
        [SerializeField] private float trackMaxAgeSeconds = 2.5f;

        [Header("Mapping")]
        [SerializeField] private ViewportRaycaster raycaster;
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private SnapshotFrameStore snapshotStore;
        [SerializeField] private Camera snapshotCamera;
        [SerializeField] private float snapshotDistance = 1.0f;
        [SerializeField] private float minScore = 0.3f;
        [SerializeField] private int maxPinsPerCapture = 20;

        /// <summary>
        /// The GroundingDINO prompt used whenever Florence has not listed the snapshot's objects:
        /// common room objects, the same list the CI Spatial Companion app uses, so both apps look
        /// for the same objects.
        /// </summary>
        public const string DefaultGroundingDinoPrompt =
            "chair . table . sofa . bed . lamp . cup . mug . bottle . bowl . book . laptop . monitor . " +
            "keyboard . phone . remote control . plant . vase . clock . picture frame . shelf . cabinet . " +
            "door . window . box . bag . pillow .";

        [Header("Prompts")]
        [FormerlySerializedAs("detectionPrompt")]
        [Tooltip("The GroundingDINO prompt whenever Florence has not listed the snapshot's objects " +
                 "(with the GDino preset, every capture), and SAM2's last fallback. " +
                 "Blank uses the default list of common room objects.")]
        [SerializeField] private string groundingDinoFallbackPrompt = DefaultGroundingDinoPrompt;
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
        [Tooltip("The scene backend: any component that implements ISceneSource (MetaSceneCollector on Quest).")]
        [SerializeField] private MonoBehaviour metaSceneCollector;
        [SerializeField] private bool runMetaSceneCapture = true;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Runtime state
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private ICameraProvider _cameraProvider;
        private ICameraProvider _cameraSourceFor;
        private ICameraSource _cameraSource;
        private ISceneSource _sceneSourceOverride;
        private CVServerClient _client;
        private bool _isBusy;
        private bool _loggedStaticCameraPose;
        private Texture2D _frameTexture;
        private CaptureFrame _lastCaptureFrame;
        private TrackTable _tracks;
        private string _captureSessionId;
        private int _captureFrameIndex;
        private bool _lastCaptureHadIntrinsics;
        private int _lastFallbackHits;
        private bool _loggedMissingIntrinsics;

        /// <summary>A pin made from one detection, with the detection as the tracker sees it.</summary>
        private struct PendingPin
        {
            public SpatialPin pin;
            public Detection3D detection;
        }

        /// <summary>
        /// The objects found so far, matched across captures: the same object seen again is one track, so
        /// one pin that is updated, not a second pin. The tracker is the <c>Companion.Spatial</c> package's:
        /// same canonical tag, within <see cref="trackDistanceThreshold"/>, positions smoothed with
        /// <see cref="smoothingAlpha"/>. A track that is not seen for <see cref="trackMaxAgeSeconds"/> is
        /// lost, not forgotten: seen again later it is the same object (it used to be forgotten after that
        /// time, so a second capture a few seconds later pinned every object twice).
        /// </summary>
        private TrackTable Tracks
        {
            get
            {
                if (_tracks == null)
                {
                    _tracks = new TrackTable(new TrackTableOptions(
                        associationDistance: Mathf.Max(0.01f, trackDistanceThreshold),
                        smoothingAlpha: enableTemporalSmoothing ? Mathf.Clamp01(smoothingAlpha) : 1.0,
                        lostAfterSeconds: Mathf.Max(0.1f, trackMaxAgeSeconds)));
                }

                return _tracks;
            }
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

        /// <summary>
        /// What GroundingDINO is asked to find when Florence has not listed the snapshot's objects
        /// (with the GDino preset, every capture), and SAM2's last fallback: the prompt field, or
        /// <see cref="DefaultGroundingDinoPrompt"/> when the field is blank.
        /// </summary>
        public string GroundingDinoFallbackPrompt =>
            string.IsNullOrWhiteSpace(groundingDinoFallbackPrompt)
                ? DefaultGroundingDinoPrompt
                : groundingDinoFallbackPrompt.Trim();

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

            // Every pin gets a 3D label, Space Setup and vision server pins alike: the label
            // renderer follows the pin manager. Connect them if the scene did not.
            if (labelRenderer != null && pinManager != null && labelRenderer.PinManager == null)
            {
                labelRenderer.Follow(pinManager);
            }
            // A camera or scene backend given with UseCameraProvider / UseSceneSource before Awake wins
            // over the serialized one.
#if UNITY_ANDROID
            if (preferArCameraOnQuest && _cameraProvider == null)
            {
                TryAutoSetupArCameraProvider();
            }
#endif
            _cameraProvider = _cameraProvider ?? (cameraProviderSource as ICameraProvider);
        }

        /// <summary>
        /// Uses <paramref name="provider"/> as the camera, in place of the one the scene serializes (the
        /// replay backend, a test's fake). Capture is stopped on the old camera and started on the new one
        /// while this component is enabled. Plain <see cref="ICameraProvider"/>s are adapted; an
        /// <see cref="ICameraSource"/> is used as it is.
        /// </summary>
        public void UseCameraProvider(ICameraProvider provider)
        {
            if (ReferenceEquals(_cameraProvider, provider))
            {
                return;
            }

            if (isActiveAndEnabled)
            {
                _cameraProvider?.StopCapture();
            }

            _cameraProvider = provider;
            if (isActiveAndEnabled)
            {
                _cameraProvider?.StartCapture();
            }
        }

        /// <summary>Uses <paramref name="source"/> as the scene backend, in place of the component the scene serializes.</summary>
        public void UseSceneSource(ISceneSource source)
        {
            _sceneSourceOverride = source;
        }

        /// <summary>The scene backend in use: the one given with <see cref="UseSceneSource"/>, else the serialized component, else null.</summary>
        public ISceneSource SceneSource =>
            _sceneSourceOverride ?? (metaSceneCollector != null ? metaSceneCollector as ISceneSource : null);

        /// <summary>The camera in use as an <see cref="ICameraSource"/> (an adapter for a plain provider); null without a camera.</summary>
        private ICameraSource CameraSource
        {
            get
            {
                if (!ReferenceEquals(_cameraSourceFor, _cameraProvider))
                {
                    _cameraSource = CameraSources.From(_cameraProvider);
                    _cameraSourceFor = _cameraProvider;
                }

                return _cameraSource;
            }
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
                // Assembled inactive, with the ARCameraManager disabled: enabling it starts Meta
                // OpenXR's camera subsystem, which checks the headset camera permission only then.
                // MobileCameraProvider enables it once the permissions are granted.
                cameraGo.SetActive(false);
                cameraGo.transform.SetParent(transform, false);
                var camera = cameraGo.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.Nothing;
                camera.cullingMask = 0;
                var cameraManager = cameraGo.AddComponent<ARCameraManager>();
                cameraManager.enabled = false;
                cameraGo.AddComponent<ARCameraBackground>();
                existingProvider = cameraGo.AddComponent<MobileCameraProvider>();
                cameraGo.SetActive(true);
                Debug.Log("[Pipeline] Created ARSession + PCA camera for passthrough capture.");
            }

            // The camera object sits under this root, which never moves: the provider reports the
            // pose of the camera that follows the headset instead of its own.
            var tracked = snapshotCamera != null ? snapshotCamera : Camera.main;
            existingProvider.EnsurePoseSource(tracked != null ? tracked.transform : null);
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

                // Meta Scene (fast, local, no network). A failed Space Setup read must not stop
                // the capture the user asked for.
                if (runMetaSceneCapture)
                {
                    try
                    {
                        await RunMetaSceneCaptureAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Pipeline-Meta] Reading Space Setup failed: {ex.Message}");
                    }
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
            var sceneSource = SceneSource;
            if (sceneSource == null)
            {
                Debug.LogWarning("[Pipeline-Meta] MetaSceneCollector is not assigned.");
                return;
            }

            Debug.Log("[Pipeline-Meta] Querying Meta Scene Understandingâ€¦");
            var snapshot = await sceneSource.CaptureSnapshotAsync();
            LastMetaSnapshot = snapshot;

            if (!string.IsNullOrEmpty(snapshot.error))
            {
                Debug.LogWarning($"[Pipeline-Meta] {snapshot.error}");
                return;
            }

            Debug.Log($"[Pipeline-Meta] Captured {snapshot.anchors.Count} scene anchors.");

            // Convert to SpatialPins and add to the shared pin manager
            var pins = sceneSource.ConvertSnapshotToPins(snapshot);
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

            if (!serverConfig.HasBaseUrl)
            {
                Debug.LogWarning("[Pipeline-CV] CV server URL is not set; skipping server detection. " +
                                 "Press the left Menu button to enter it.");
                return;
            }

            if (!serverConfig.IsUrlAllowed(serverConfig.ResolveBaseUrl(), out var policyError))
            {
                Debug.LogWarning($"[Pipeline-CV] {policyError}");
                return;
            }

            // No snapshot leaves the headset before the user has agreed, in the camera notice, to
            // send snapshots to this server. Until then nothing is captured; when they agree, the
            // notice runs the CV pipeline.
            if (!CaptureConsent.EnsureGranted(serverConfig.ResolveBaseUrl(), CaptureAndPin))
            {
                Debug.Log("[Pipeline-CV] Waiting for agreement to send camera snapshots; nothing was captured or sent.");
                return;
            }

            // Pin placement prefers environment raycasts, which need the spatial
            // data permission: ask before the first capture (no-op once answered;
            // when denied the raycaster falls back to physics/fixed-distance hits).
            await SpatialDataPermission.RequestAsync();

            if (!CameraSource.TryCaptureFrame(jpegQuality, out var cameraFrame))
            {
                Debug.LogWarning("[Pipeline-CV] Failed to capture frame from camera.");
                return;
            }

            var frame = cameraFrame.Jpeg;

            Debug.Log($"[Pipeline-CV] Captured frame ({frame.Length} bytes). Starting CV pipelineâ€¦");
            EnsureFrameTexture(frame);
            var capture = BuildCaptureFrame(cameraFrame);
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
            // The label renderer follows the pin manager, so each pin added above already has its
            // one label. (Re-rendering every pin here put another copy of each label in the room
            // after every capture, and those copies did not move with their pins.)
            if (labelRenderer != null && pinManager != null)
            {
                Debug.Log($"[Pipeline-CV] {pinManager.CvPins.Count} vision server pins, " +
                          $"{labelRenderer.PinLabelCount} 3D labels in the room.");
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
            return GroundingDinoFallbackPrompt;
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

            return GroundingDinoFallbackPrompt;
        }

        private void EnsureFrameTexture(byte[] jpegBytes)
        {
            if (_frameTexture == null)
            {
                _frameTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            }
            _frameTexture.LoadImage(jpegBytes);
        }

        private CaptureFrame BuildCaptureFrame(CameraFrame source)
        {
            Pose pose = source.Pose;

            if (!source.PoseIsGuaranteed)
            {
                // A plain camera provider that reports the world origin while the headset is elsewhere
                // is a transform that does not follow the headset: stamp the headset's pose instead.
                // (An ICameraSource promises the pose at capture, whatever it is.)
                var cam = snapshotCamera != null ? snapshotCamera : Camera.main;
                Pose cameraPose = cam != null
                    ? new Pose(cam.transform.position, cam.transform.rotation)
                    : new Pose(Vector3.zero, Quaternion.identity);
                pose = CapturePose.Resolve(source.Pose, cam != null, cameraPose, out bool providerRejected);
                if (providerRejected && !_loggedStaticCameraPose)
                {
                    _loggedStaticCameraPose = true;
                    Debug.LogWarning("[Pipeline] The camera provider reported the world origin while the headset is elsewhere; capture frames carry the headset's pose instead.");
                }
            }

            // The source's own size when it gives one; otherwise the decoded image's.
            int width = source.Width > 0 ? source.Width : (_frameTexture != null ? _frameTexture.width : 0);
            int height = source.Height > 0 ? source.Height : (_frameTexture != null ? _frameTexture.height : 0);

            var capture = CaptureFrame.Create(source.Jpeg, width, height, pose, source.Intrinsics);
            if (source.Timestamp > 0.0)
            {
                capture.timestamp = source.Timestamp; // when the frame was taken; otherwise the time of this call
            }

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

            var pending = new List<PendingPin>();
            foreach (var ann in segmentation.annotations)
            {
                if (pending.Count >= maxPinsPerCapture) break;
                if (ann.score < minScore) continue;
                if (TryMakePin(ann.label, ann.score, ann.bbox, "segment", capture, out var made))
                {
                    pending.Add(made);
                }
            }

            AddTrackedPins(pending, capture);
            Debug.Log($"[Pipeline-CV] Created {pending.Count} spatial pins from SAM2 annotations.");
        }

        private void AddCvPinsFromDetections(DetectionResult detection, CaptureFrame capture)
        {
            if (detection == null || detection.bboxes == null || raycaster == null || pinManager == null)
            {
                return;
            }

            int count = Mathf.Min(detection.bboxes.Count, detection.labels.Count);
            var pending = new List<PendingPin>();

            for (int i = 0; i < count; i++)
            {
                if (pending.Count >= maxPinsPerCapture) break;

                float score = i < detection.scores.Count ? detection.scores[i] : 0f;
                if (score < minScore) continue;

                if (TryMakePin(detection.labels[i], score, detection.bboxes[i], BuildDinoPrompt(null), capture, out var made))
                {
                    pending.Add(made);
                }
            }

            AddTrackedPins(pending, capture);
        }

        /// <summary>
        /// Makes the pin for one detection: the box becomes a ray through the capture's camera, the ray a
        /// place in the room, and the place the pin's position (before it is matched to an earlier object).
        /// False when the box is no box.
        /// </summary>
        private bool TryMakePin(string label, float score, float[] bbox, string prompt, CaptureFrame capture, out PendingPin made)
        {
            made = default;
            if (bbox == null || bbox.Length < 4)
            {
                return false;
            }

            int imgW = capture != null ? capture.imageWidth : (_frameTexture != null ? _frameTexture.width : 0);
            int imgH = capture != null ? capture.imageHeight : (_frameTexture != null ? _frameTexture.height : 0);
            if (!BoundingBoxUtils.TryNormalize(bbox, imgW, imgH, out Rect normalized))
            {
                return false;
            }

            float u = normalized.x + normalized.width * 0.5f;
            float v = 1.0f - (normalized.y + normalized.height * 0.5f);
            Rect pixelRect = BoundingBoxUtils.NormalizedToPixels(normalized, imgW, imgH);

            var pixel = new Vector2(pixelRect.x + pixelRect.width * 0.5f,
                                    pixelRect.y + pixelRect.height * 0.5f);

            if (!TryResolveHit(capture, new Vector2(u, v), pixel, out Vector3 hitPoint, out Vector3 hitNormal, out RaycastHitType hitType))
            {
                hitNormal = Vector3.up;
            }

            double timestamp = capture != null ? capture.timestamp : Time.timeAsDouble;
            made = new PendingPin
            {
                pin = new SpatialPin
                {
                    label = label,
                    semanticLabel = SemanticLabelMapper.MapToMetaLabel(label),
                    score = score,
                    source = PinSource.CvServer,
                    position = hitPoint,
                    normal = hitNormal,
                    bboxPixels = new SerializableRect(pixelRect.x, pixelRect.y, pixelRect.width, pixelRect.height),
                    bboxNormalized = new SerializableRect(normalized.x, normalized.y, normalized.width, normalized.height),
                    sourcePrompt = prompt,
                    timestamp = timestamp,
                    captureId = capture?.id,
                    captureSessionId = capture?.sessionId,
                    captureFrameIndex = capture?.frameIndex ?? 0,
                    placementConfidence = ComputePlacementConfidence(score, hitType),
                    hitType = hitType
                },
                detection = new Detection3D(label, score, ToVec3(hitPoint), ToVec3(hitNormal), capture?.id,
                    NormalizedBox.TryCreate(normalized.xMin, normalized.yMin, normalized.xMax, normalized.yMax, out var box)
                        ? box
                        : (NormalizedBox?)null)
            };
            return true;
        }

        /// <summary>
        /// Matches one capture's detections to the objects found so far, then adds a pin for each: the pin
        /// of an object seen before takes that object's track id and its smoothed position, so it replaces
        /// the earlier pin. Two detections of one capture are never one object. A detection the tracker
        /// cannot use (no label) is pinned where it was found, with no track.
        /// </summary>
        private void AddTrackedPins(List<PendingPin> pending, CaptureFrame capture)
        {
            if (pending.Count == 0)
            {
                return;
            }

            var detections = new List<Detection3D>(pending.Count);
            foreach (var item in pending)
            {
                detections.Add(item.detection);
            }

            var table = Tracks;
            var trackOf = new int[pending.Count];
            foreach (var trackEvent in table.Update(capture != null ? capture.timestamp : Time.timeAsDouble, detections))
            {
                bool forADetection = trackEvent.Kind == TrackEventKind.Found
                                     || trackEvent.Kind == TrackEventKind.Updated
                                     || trackEvent.Kind == TrackEventKind.Revived;
                if (forADetection && trackEvent.DetectionIndex >= 0 && trackEvent.DetectionIndex < trackOf.Length)
                {
                    trackOf[trackEvent.DetectionIndex] = trackEvent.TrackId;
                }
            }

            for (int i = 0; i < pending.Count; i++)
            {
                var pin = pending[i].pin;
                if (trackOf[i] > 0 && table.TryGetTrack(trackOf[i], out var track))
                {
                    pin.trackId = track.Id;
                    pin.position = ToVector3(track.Position);
                }

                pinManager.AddPin(pin);
            }
        }

        private static Vec3 ToVec3(Vector3 v) => new Vec3(v.x, v.y, v.z);

        private static Vector3 ToVector3(Vec3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

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

        private bool TryProjectToSemanticPlane(CaptureFrame capture, Vector2 pixel, out Vector3 point, out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.up;

            if (SceneSource?.LastSnapshot == null)
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

            var lastSnapshot = SceneSource?.LastSnapshot;
            if (!preferSemanticPlanes || lastSnapshot == null)
            {
                return false;
            }

            float bestT = float.MaxValue;
            MetaSceneAnchorSnapshot bestAnchor = null;

            foreach (var anchor in lastSnapshot.anchors)
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

