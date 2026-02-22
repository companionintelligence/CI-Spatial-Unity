using System;
using HouseMapper.Export;
using HouseMapper.Mapping;
using HouseMapper.Rendering;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HouseMapper.UI
{
    /// <summary>
    /// World-space controller menu panel with labeled buttons for:
    /// - Capture (CV pipeline / Meta Scene / Both)
    /// - Model toggles (Florence, GroundingDINO, SAM2)
    /// - Display mode cycling (BoundingBox2D / SpatialLabel3D / Both)
    /// - Save &amp; Export
    ///
    /// Attach this to a Canvas that is a child of the left controller anchor
    /// (e.g. LeftControllerAnchor → Canvas → ControllerMenuUI).
    /// </summary>
    public class ControllerMenuUI : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════
        //  External references — set in Inspector
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
        //  Button references — set in Inspector (world-space UI Buttons)
        // ═══════════════════════════════════════════════════════════════

        [Header("Buttons")]
        [SerializeField] private Button captureAllButton;
        [SerializeField] private Button captureCvButton;
        [SerializeField] private Button captureMetaButton;
        [SerializeField] private Button saveExportButton;

        [Header("Toggle Buttons")]
        [SerializeField] private Button toggleFlorenceButton;
        [SerializeField] private Button toggleDinoButton;
        [SerializeField] private Button toggleSam2Button;
        [SerializeField] private Button toggleDisplayModeButton;

        // ═══════════════════════════════════════════════════════════════
        //  Status label references
        // ═══════════════════════════════════════════════════════════════

        [Header("Status Labels")]
        [SerializeField] private TMP_Text florenceStatusLabel;
        [SerializeField] private TMP_Text dinoStatusLabel;
        [SerializeField] private TMP_Text sam2StatusLabel;
        [SerializeField] private TMP_Text displayModeLabel;
        [SerializeField] private TMP_Text busyLabel;

        // ═══════════════════════════════════════════════════════════════
        //  State
        // ═══════════════════════════════════════════════════════════════

        private bool _florenceEnabled = true;
        private bool _dinoEnabled = true;
        private bool _sam2Enabled = true;
        private AnchorDisplayMode _displayMode = AnchorDisplayMode.Both;

        /// <summary>Current display mode for anchor visualisation.</summary>
        public AnchorDisplayMode DisplayMode => _displayMode;

        // ═══════════════════════════════════════════════════════════════
        //  Lifecycle
        // ═══════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            // Wire buttons
            if (captureAllButton != null) captureAllButton.onClick.AddListener(OnCaptureAll);
            if (captureCvButton != null) captureCvButton.onClick.AddListener(OnCaptureCv);
            if (captureMetaButton != null) captureMetaButton.onClick.AddListener(OnCaptureMeta);
            if (saveExportButton != null) saveExportButton.onClick.AddListener(OnSaveExport);

            if (toggleFlorenceButton != null) toggleFlorenceButton.onClick.AddListener(OnToggleFlorence);
            if (toggleDinoButton != null) toggleDinoButton.onClick.AddListener(OnToggleDino);
            if (toggleSam2Button != null) toggleSam2Button.onClick.AddListener(OnToggleSam2);
            if (toggleDisplayModeButton != null) toggleDisplayModeButton.onClick.AddListener(OnCycleDisplayMode);

            RefreshLabels();
        }

        private void OnDisable()
        {
            if (captureAllButton != null) captureAllButton.onClick.RemoveListener(OnCaptureAll);
            if (captureCvButton != null) captureCvButton.onClick.RemoveListener(OnCaptureCv);
            if (captureMetaButton != null) captureMetaButton.onClick.RemoveListener(OnCaptureMeta);
            if (saveExportButton != null) saveExportButton.onClick.RemoveListener(OnSaveExport);

            if (toggleFlorenceButton != null) toggleFlorenceButton.onClick.RemoveListener(OnToggleFlorence);
            if (toggleDinoButton != null) toggleDinoButton.onClick.RemoveListener(OnToggleDino);
            if (toggleSam2Button != null) toggleSam2Button.onClick.RemoveListener(OnToggleSam2);
            if (toggleDisplayModeButton != null) toggleDisplayModeButton.onClick.RemoveListener(OnCycleDisplayMode);
        }

        private void Update()
        {
            // Update busy indicator
            if (busyLabel != null && pipeline != null)
            {
                busyLabel.text = pipeline.IsBusy ? "BUSY…" : "";
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Capture actions
        // ═══════════════════════════════════════════════════════════════

        private void OnCaptureAll()
        {
            if (pipeline == null || pipeline.IsBusy) return;
            ApplyModelToggles();
            pipeline.CaptureAll();
        }

        private void OnCaptureCv()
        {
            if (pipeline == null || pipeline.IsBusy) return;
            ApplyModelToggles();
            pipeline.CaptureAndPin();
        }

        private void OnCaptureMeta()
        {
            if (pipeline == null || pipeline.IsBusy) return;
            pipeline.CaptureMetaScene();
        }

        // ═══════════════════════════════════════════════════════════════
        //  Save / Export
        // ═══════════════════════════════════════════════════════════════

        private void OnSaveExport()
        {
            if (exporter == null) return;
            Debug.Log("[ControllerMenu] Exporting + syncing…");
            exporter.ExportAndSync();
        }

        // ═══════════════════════════════════════════════════════════════
        //  Model toggles
        // ═══════════════════════════════════════════════════════════════

        private void OnToggleFlorence()
        {
            _florenceEnabled = !_florenceEnabled;
            RefreshLabels();
        }

        private void OnToggleDino()
        {
            _dinoEnabled = !_dinoEnabled;
            RefreshLabels();
        }

        private void OnToggleSam2()
        {
            _sam2Enabled = !_sam2Enabled;
            RefreshLabels();
        }

        /// <summary>
        /// Push the current toggle state into the pipeline's serialised fields
        /// before triggering a capture.
        /// </summary>
        private void ApplyModelToggles()
        {
            if (pipeline == null) return;
            pipeline.RunFlorence = _florenceEnabled;
            pipeline.RunGroundingDino = _dinoEnabled;
            pipeline.RunSam2 = _sam2Enabled;
        }

        // ═══════════════════════════════════════════════════════════════
        //  Display mode
        // ═══════════════════════════════════════════════════════════════

        private void OnCycleDisplayMode()
        {
            _displayMode = _displayMode switch
            {
                AnchorDisplayMode.BoundingBox2D => AnchorDisplayMode.SpatialLabel3D,
                AnchorDisplayMode.SpatialLabel3D => AnchorDisplayMode.Both,
                AnchorDisplayMode.Both => AnchorDisplayMode.BoundingBox2D,
                _ => AnchorDisplayMode.Both
            };

            ApplyDisplayMode();
            RefreshLabels();
        }

        /// <summary>Set display mode programmatically (e.g. from another UI).</summary>
        public void SetDisplayMode(AnchorDisplayMode mode)
        {
            _displayMode = mode;
            ApplyDisplayMode();
            RefreshLabels();
        }

        private void ApplyDisplayMode()
        {
            bool show2D = _displayMode == AnchorDisplayMode.BoundingBox2D ||
                          _displayMode == AnchorDisplayMode.Both;
            bool show3D = _displayMode == AnchorDisplayMode.SpatialLabel3D ||
                          _displayMode == AnchorDisplayMode.Both;

            if (bboxRenderer != null) bboxRenderer.gameObject.SetActive(show2D);
            if (labelRenderer != null) labelRenderer.gameObject.SetActive(show3D);

            // Pipeline also needs to know so it can skip bbox/label creation
            if (pipeline != null)
            {
                pipeline.RenderBoxes = show2D;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Label refresh
        // ═══════════════════════════════════════════════════════════════

        private void RefreshLabels()
        {
            SetToggleLabel(florenceStatusLabel, "Florence2", _florenceEnabled);
            SetToggleLabel(dinoStatusLabel, "GDino", _dinoEnabled);
            SetToggleLabel(sam2StatusLabel, "SAM2", _sam2Enabled);

            if (displayModeLabel != null)
            {
                displayModeLabel.text = _displayMode switch
                {
                    AnchorDisplayMode.BoundingBox2D => "Display: 2D Box",
                    AnchorDisplayMode.SpatialLabel3D => "Display: 3D Label",
                    AnchorDisplayMode.Both => "Display: Both",
                    _ => "Display: ?"
                };
            }
        }

        private static void SetToggleLabel(TMP_Text label, string name, bool isOn)
        {
            if (label == null) return;
            label.text = isOn ? $"{name}: ON" : $"{name}: OFF";
            label.color = isOn ? Color.green : new Color(1f, 0.3f, 0.3f);
        }
    }
}
