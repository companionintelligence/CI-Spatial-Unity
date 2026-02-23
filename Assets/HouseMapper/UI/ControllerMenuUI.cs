using System;
using HouseMapper.Export;
using HouseMapper.Mapping;
using HouseMapper.Rendering;
using TMPro;
using UnityEngine;

namespace HouseMapper.UI
{
    /// <summary>
    /// Manages controller-driven state for the Quest XR UI.
    /// All actions are invoked by <see cref="XRControllerInput"/> via public
    /// methods — no world-space UI buttons.
    ///
    /// Button mapping:
    ///   A  (red)    → <see cref="DoCapture"/>
    ///   B  (blue)   → <see cref="CycleModel"/>
    ///   Y  (green)  → <see cref="DoSaveExport"/>
    ///   X  (yellow) → <see cref="CyclePreviewDistance"/>
    ///   Trigger (orange) → <see cref="CycleDisplayMode"/>
    /// </summary>
    public class ControllerMenuUI : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════
        //  External references (auto-discovered or injected by builder)
        // ═══════════════════════════════════════════════════════════════

        [Header("Pipeline")]
        [SerializeField] private SpatialCapturePipeline pipeline;
        [SerializeField] private SpatialMapExporter exporter;

        [Header("Renderers")]
        [SerializeField] private BBoxRenderer bboxRenderer;
        [SerializeField] private MaskRenderer maskRenderer;
        [SerializeField] private LabelRenderer labelRenderer;
        [SerializeField] private CaptionPanel captionPanel;

        // ═══════════════════════════════════════════════════════════════
        //  HUD labels (set by ControllerMenuBuilder)
        // ═══════════════════════════════════════════════════════════════

        [Header("HUD Labels")]
        [SerializeField] private TMP_Text modelLabel;
        [SerializeField] private TMP_Text displayModeLabel;
        [SerializeField] private TMP_Text previewDistanceLabel;
        [SerializeField] private TMP_Text busyLabel;

        // ═══════════════════════════════════════════════════════════════
        //  Model cycle enum
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Active model preset cycled by the B button.</summary>
        public enum ModelPreset { Florence, GDino, SAM2, All }

        // ═══════════════════════════════════════════════════════════════
        //  Preview distance enum
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Preview distance cycled by the X button.</summary>
        public enum PreviewDistance { Near, Far, Off }

        // ═══════════════════════════════════════════════════════════════
        //  State
        // ═══════════════════════════════════════════════════════════════

        private ModelPreset _activeModel = ModelPreset.All;
        private AnchorDisplayMode _displayMode = AnchorDisplayMode.Both;
        private PreviewDistance _previewDist = PreviewDistance.Near;

        /// <summary>Current model preset.</summary>
        public ModelPreset ActiveModel => _activeModel;

        /// <summary>Current display mode for anchor visualisation.</summary>
        public AnchorDisplayMode DisplayMode => _displayMode;

        /// <summary>Current preview distance setting.</summary>
        public PreviewDistance CurrentPreviewDistance => _previewDist;

        // ═══════════════════════════════════════════════════════════════
        //  Lifecycle
        // ═══════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            ApplyModelPreset();
            ApplyDisplayMode();
            RefreshHUD();
        }

        private void Update()
        {
            if (busyLabel != null && pipeline != null)
                busyLabel.text = pipeline.IsBusy ? "BUSY…" : "";
        }

        // ═══════════════════════════════════════════════════════════════
        //  Public API — called by XRControllerInput
        // ═══════════════════════════════════════════════════════════════

        /// <summary>A button — take a capture snapshot.</summary>
        public void DoCapture()
        {
            if (pipeline == null || pipeline.IsBusy) return;
            ApplyModelPreset();
            ApplyPreviewDistance();
            Debug.Log($"[ControllerMenu] Capture → model={_activeModel}");
            pipeline.CaptureAll();
        }

        /// <summary>B button — cycle active model preset.</summary>
        public void CycleModel()
        {
            _activeModel = _activeModel switch
            {
                ModelPreset.Florence => ModelPreset.GDino,
                ModelPreset.GDino   => ModelPreset.SAM2,
                ModelPreset.SAM2    => ModelPreset.All,
                ModelPreset.All     => ModelPreset.Florence,
                _ => ModelPreset.All
            };
            ApplyModelPreset();
            RefreshHUD();
            Debug.Log($"[ControllerMenu] Model → {_activeModel}");
        }

        /// <summary>X button — cycle preview distance (near / far / off).</summary>
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
            Debug.Log($"[ControllerMenu] Preview distance → {_previewDist}");
        }

        /// <summary>Y button — save &amp; export GLTF.</summary>
        public void DoSaveExport()
        {
            if (exporter == null) return;
            Debug.Log("[ControllerMenu] Exporting + syncing…");
            exporter.ExportAndSync();
        }

        /// <summary>Trigger — cycle display mode (BBox2D → Label3D → Both).</summary>
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
            Debug.Log($"[ControllerMenu] Display → {_displayMode}");
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

        // ═══════════════════════════════════════════════════════════════
        //  Internal — apply state
        // ═══════════════════════════════════════════════════════════════

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
                case ModelPreset.All:
                default:
                    pipeline.RunFlorence      = true;
                    pipeline.RunGroundingDino  = true;
                    pipeline.RunSam2           = true;
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

            if (pipeline != null) pipeline.RenderBoxes = show2D;
        }

        private void ApplyPreviewDistance()
        {
            if (pipeline == null || pipeline.Raycaster == null) return;
            float metres = GetPreviewDistanceMetres();
            if (metres > 0f)
                pipeline.Raycaster.FallbackDistance = metres;
        }

        // ═══════════════════════════════════════════════════════════════
        //  HUD refresh
        // ═══════════════════════════════════════════════════════════════

        private void RefreshHUD()
        {
            if (modelLabel != null)
            {
                modelLabel.text = _activeModel switch
                {
                    ModelPreset.Florence => "Model: Florence",
                    ModelPreset.GDino   => "Model: GDino",
                    ModelPreset.SAM2    => "Model: SAM2",
                    ModelPreset.All     => "Model: All",
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
        }
    }
}
