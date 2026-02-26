using System;
using CompanionScan.Export;
using CompanionScan.Mapping;
using CompanionScan.Rendering;
using TMPro;
using UnityEngine;

namespace CompanionScan.UI
{
    /// <summary>
    /// Manages controller-driven state for the Quest XR UI.
    /// All actions are invoked by <see cref="XRControllerInput"/> via public
    /// methods â€” no world-space UI buttons.
    ///
    /// Button mapping:
    ///   A  (red)    -> <see cref="DoCapture"/>
    ///   B  (blue)   -> <see cref="CycleModel"/>
    ///   Y  (green)  -> <see cref="DoSaveExport"/>
    ///   X  (yellow) -> <see cref="CyclePreviewDistance"/>
    ///   Trigger (orange) -> <see cref="CycleDisplayMode"/>
    ///   Inner trigger -> <see cref="PreviewGltf"/>
    /// </summary>
    public class ControllerMenuUI : MonoBehaviour
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  External references (auto-discovered or injected by builder)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("Pipeline")]
        [SerializeField] private SpatialCapturePipeline pipeline;
        [SerializeField] private SpatialMapExporter exporter;

        [Header("Renderers")]
        [SerializeField] private BBoxRenderer bboxRenderer;
        [SerializeField] private MaskRenderer maskRenderer;
        [SerializeField] private LabelRenderer labelRenderer;
        [SerializeField] private CaptionPanel captionPanel;
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private SnapshotFrustumVisualizer frustumVisualizer;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  HUD labels (set by ControllerMenuBuilder)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("HUD Labels")]
        [SerializeField] private TMP_Text modelLabel;
        [SerializeField] private TMP_Text displayModeLabel;
        [SerializeField] private TMP_Text previewDistanceLabel;
        [SerializeField] private TMP_Text busyLabel;
        [SerializeField] private TMP_Text statusLabel;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Model cycle enum
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Active model preset cycled by the B button.</summary>
        public enum ModelPreset { Florence, GDino, SAM2 }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Preview distance enum
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Preview distance cycled by the X button.</summary>
        public enum PreviewDistance { Near, Far, Off }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  State
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private ModelPreset _activeModel = ModelPreset.Florence;
        private AnchorDisplayMode _displayMode = AnchorDisplayMode.Both;
        private PreviewDistance _previewDist = PreviewDistance.Near;

        /// <summary>Current model preset.</summary>
        public ModelPreset ActiveModel => _activeModel;

        /// <summary>Current display mode for anchor visualisation.</summary>
        public AnchorDisplayMode DisplayMode => _displayMode;

        /// <summary>Current preview distance setting.</summary>
        public PreviewDistance CurrentPreviewDistance => _previewDist;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Lifecycle
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void OnEnable()
        {
            EnsureHudLabels();
            ApplyModelPreset();
            ApplyDisplayMode();
            RefreshHUD();
        }

        private void Update()
        {
            if (busyLabel != null && pipeline != null)
                busyLabel.text = pipeline.IsBusy ? "BUSYâ€¦" : "";
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Public API â€” called by XRControllerInput
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>A button â€” take a capture snapshot.</summary>
        public void DoCapture()
        {
            if (pipeline == null)
            {
                Debug.LogWarning("[ControllerMenu] DoCapture â€” pipeline is null, cannot capture.");
                return;
            }
            if (pipeline.IsBusy)
            {
                Debug.Log("[ControllerMenu] DoCapture â€” pipeline is busy, skipping.");
                return;
            }
            ApplyModelPreset();
            ApplyPreviewDistance();
            if (pipeline != null)
            {
                pipeline.SnapshotDistance = GetPreviewDistanceMetres();
            }
            if (frustumVisualizer != null)
            {
                frustumVisualizer.FreezeFrustum(GetPreviewDistanceMetres());
            }
            Debug.Log($"[ControllerMenu] Capture â†’ model={_activeModel} display={_displayMode}");
            pipeline.CaptureAll();
        }

        /// <summary>B button â€” cycle active model preset.</summary>
        public void CycleModel()
        {
            _activeModel = _activeModel switch
            {
                ModelPreset.Florence => ModelPreset.GDino,
                ModelPreset.GDino   => ModelPreset.SAM2,
                ModelPreset.SAM2    => ModelPreset.Florence,
                _ => ModelPreset.Florence
            };
            ApplyModelPreset();
            RefreshHUD();
            Debug.Log($"[ControllerMenu] Model â†’ {_activeModel}");
        }

        /// <summary>X button â€” cycle preview distance (near / far / off).</summary>
        public void CyclePreviewDistance()
        {
            _previewDist = _previewDist switch
            {
                PreviewDistance.Near => PreviewDistance.Far,
                PreviewDistance.Far  => PreviewDistance.Off,
                PreviewDistance.Off  => PreviewDistance.Near,
                _ => PreviewDistance.Near
            };
            RefreshHUD();
            Debug.Log($"[ControllerMenu] Preview distance â†’ {_previewDist}");
        }

        /// <summary>Y button â€” save &amp; export GLTF.</summary>
        public void DoSaveExport()
        {
            if (exporter == null) return;
            Debug.Log("[ControllerMenu] Exporting + syncingâ€¦");
            exporter.ExportAndSync();
        }

        /// <summary>Trigger â€” cycle display mode (BBox2D â†’ Label3D â†’ Both).</summary>
        public void CycleDisplayMode()
        {
            _displayMode = _displayMode switch
            {
                AnchorDisplayMode.BoundingBox2D => AnchorDisplayMode.SpatialLabel3D,
                AnchorDisplayMode.SpatialLabel3D => AnchorDisplayMode.Both,
                AnchorDisplayMode.Both => AnchorDisplayMode.BoundingBox2D,
                _ => AnchorDisplayMode.Both
            };
            ApplyDisplayMode();
            RefreshHUD();
            Debug.Log($"[ControllerMenu] Display â†’ {_displayMode}");
        }

        /// <summary>Inner trigger â€” export a glTF preview (no upload).</summary>
        public void PreviewGltf()
        {
            if (exporter == null) return;
            Debug.Log("[ControllerMenu] Exporting glTF previewâ€¦");
            exporter.Export();
        }

        /// <summary>Set display mode programmatically (e.g. from tests).</summary>
        public void SetDisplayMode(AnchorDisplayMode mode)
        {
            _displayMode = mode;
            ApplyDisplayMode();
            RefreshHUD();
        }

        /// <summary>Returns the world-space distance used for preview placement.</summary>
        public float GetPreviewDistanceMetres()
        {
            return _previewDist switch
            {
                PreviewDistance.Near => 1.0f,
                PreviewDistance.Far  => 3.0f,
                PreviewDistance.Off  => 0f,
                _ => 1.0f
            };
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Internal â€” apply state
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ApplyModelPreset()
        {
            if (pipeline == null) return;
            switch (_activeModel)
            {
                case ModelPreset.Florence:
                    pipeline.RunFlorence      = true;
                    pipeline.RunGroundingDino  = false;
                    pipeline.RunSam2           = false;
                    break;
                case ModelPreset.GDino:
                    pipeline.RunFlorence      = false;
                    pipeline.RunGroundingDino  = true;
                    pipeline.RunSam2           = false;
                    break;
                case ModelPreset.SAM2:
                    pipeline.RunFlorence      = false;
                    pipeline.RunGroundingDino  = false;
                    pipeline.RunSam2           = true;
                    break;
                default:
                    pipeline.RunFlorence      = true;
                    pipeline.RunGroundingDino  = false;
                    pipeline.RunSam2           = false;
                    break;
            }
        }

        private void ApplyDisplayMode()
        {
            bool show2D = _displayMode is AnchorDisplayMode.BoundingBox2D
                       or AnchorDisplayMode.Both;
            bool show3D = _displayMode is AnchorDisplayMode.SpatialLabel3D
                       or AnchorDisplayMode.Both;

            if (bboxRenderer != null) bboxRenderer.gameObject.SetActive(show2D);
            if (labelRenderer != null) labelRenderer.gameObject.SetActive(show3D);

            // Also control spatial pin visibility from SpatialPinManager
            var pm = pinManager != null ? pinManager : pipeline?.PinManager;
            if (pm != null && pm.transform != null)
            {
                // The pinRoot may be a child or the manager itself
                pm.gameObject.SetActive(show3D);
            }

            if (pipeline != null)
            {
                pipeline.RenderBoxes = show2D;
                pipeline.RenderMasks = show2D || show3D;
            }
        }

        private void ApplyPreviewDistance()
        {
            if (pipeline == null || pipeline.Raycaster == null) return;
            float metres = GetPreviewDistanceMetres();
            if (metres > 0f)
                pipeline.Raycaster.FallbackDistance = metres;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  HUD refresh
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void RefreshHUD()
        {
            EnsureHudLabels();
            if (modelLabel != null)
            {
                modelLabel.text = _activeModel switch
                {
                    ModelPreset.Florence => "Model: Florence",
                    ModelPreset.GDino   => "Model: DINO",
                    ModelPreset.SAM2    => "Model: SAM2",
                    _ => "Model: ?"
                };
            }

            if (displayModeLabel != null)
            {
                displayModeLabel.text = _displayMode switch
                {
                    AnchorDisplayMode.BoundingBox2D  => "Display: 2D Box",
                    AnchorDisplayMode.SpatialLabel3D => "Display: 3D Label",
                    AnchorDisplayMode.Both           => "Display: Both",
                    _ => "Display: ?"
                };
            }

            if (previewDistanceLabel != null)
            {
                previewDistanceLabel.text = _previewDist switch
                {
                    PreviewDistance.Near => "Preview: Near (1m)",
                    PreviewDistance.Far  => "Preview: Far (3m)",
                    PreviewDistance.Off  => "Preview: Off",
                    _ => "Preview: ?"
                };
            }

            if (statusLabel != null && pipeline != null)
            {
                string intrinsics = pipeline.LastCaptureHadIntrinsics ? "Intrinsics" : "PoseOnly";
                string fallback = pipeline.LastFallbackHitCount > 0
                    ? $"Fallback: {pipeline.LastFallbackHitCount}"
                    : "Fallback: 0";
                var config = pipeline.ServerConfig;
                string net = config != null
                    ? $"Net: {Mathf.Max(1, config.requestTimeoutSeconds)}s x{Mathf.Max(0, config.retryCount)}"
                    : "Net: ?";
                statusLabel.text = $"Raycast: {intrinsics} | {fallback} | {net}";
            }
        }

        private void EnsureHudLabels()
        {
            if (modelLabel == null) modelLabel = FindHudLabel("ModelLabel");
            if (displayModeLabel == null) displayModeLabel = FindHudLabel("DisplayLabel");
            if (previewDistanceLabel == null) previewDistanceLabel = FindHudLabel("PreviewLabel");
            if (busyLabel == null) busyLabel = FindHudLabel("BusyLabel");
            if (statusLabel == null) statusLabel = FindHudLabel("StatusLabel");
        }

        private static TMP_Text FindHudLabel(string name)
        {
            var direct = GameObject.Find(name);
            if (direct != null)
            {
                return direct.GetComponent<TMP_Text>();
            }

            foreach (var tmp in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (tmp != null && tmp.name == name)
                {
                    return tmp;
                }
            }

            return null;
        }
    }
}

