using System;
using CompanionScan.Export;
using CompanionScan.Mapping;
using CompanionScan.Rendering;
using TMPro;
using UnityEngine;

namespace CompanionScan.UI
{
    public class ControllerMenuUI : MonoBehaviour
    {
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

        [Header("HUD Labels")]
        [SerializeField] private TMP_Text modelLabel;
        [SerializeField] private TMP_Text displayModeLabel;
        [SerializeField] private TMP_Text previewDistanceLabel;
        [SerializeField] private TMP_Text busyLabel;
        [SerializeField] private TMP_Text statusLabel;

        public enum ModelPreset { Florence, GDino, SAM2 }
        public enum PreviewDistance { Near, Far, Off }

        [Serializable]
        public struct UiHealthStatus
        {
            public bool HasPipeline;
            public bool HasExporter;
            public bool HasHudLabels;
            public bool HasInputBinding;
            public int MissingHudLabels;

            public bool IsHealthy => HasPipeline && HasHudLabels && HasInputBinding;
        }

        private ModelPreset _activeModel = ModelPreset.Florence;
        private AnchorDisplayMode _displayMode = AnchorDisplayMode.Both;
        private PreviewDistance _previewDist = PreviewDistance.Near;

        public ModelPreset ActiveModel => _activeModel;
        public AnchorDisplayMode DisplayMode => _displayMode;
        public PreviewDistance CurrentPreviewDistance => _previewDist;

        private void OnEnable()
        {
            EnsureHudLabels();
            ApplyModelPreset();
            ApplyDisplayMode();
            RefreshHUD();
            LogUiHealth();
        }

        private void Update()
        {
            if (busyLabel != null && pipeline != null)
                busyLabel.text = pipeline.IsBusy ? "BUSY..." : string.Empty;
        }

        public UiHealthStatus GetUiHealthStatus()
        {
            EnsureHudLabels();

            int missingLabels = 0;
            if (modelLabel == null) missingLabels++;
            if (displayModeLabel == null) missingLabels++;
            if (previewDistanceLabel == null) missingLabels++;
            if (busyLabel == null) missingLabels++;
            if (statusLabel == null) missingLabels++;

            return new UiHealthStatus
            {
                HasPipeline = pipeline != null,
                HasExporter = exporter != null,
                HasHudLabels = missingLabels == 0,
                HasInputBinding = FindAnyObjectByType<XRControllerInput>() != null,
                MissingHudLabels = missingLabels
            };
        }

        public void DoCapture()
        {
            if (pipeline == null)
            {
                Debug.LogWarning("[ControllerMenu] DoCapture skipped: pipeline is null.");
                return;
            }

            if (pipeline.IsBusy)
            {
                Debug.Log("[ControllerMenu] DoCapture skipped: pipeline is busy.");
                return;
            }

            ApplyModelPreset();
            ApplyPreviewDistance();
            pipeline.SnapshotDistance = GetPreviewDistanceMetres();
            frustumVisualizer?.FreezeFrustum(GetPreviewDistanceMetres());
            pipeline.CaptureAll();
        }

        public void CycleModel()
        {
            _activeModel = _activeModel switch
            {
                ModelPreset.Florence => ModelPreset.GDino,
                ModelPreset.GDino => ModelPreset.SAM2,
                ModelPreset.SAM2 => ModelPreset.Florence,
                _ => ModelPreset.Florence
            };

            ApplyModelPreset();
            RefreshHUD();
        }

        public void CyclePreviewDistance()
        {
            _previewDist = _previewDist switch
            {
                PreviewDistance.Near => PreviewDistance.Far,
                PreviewDistance.Far => PreviewDistance.Off,
                PreviewDistance.Off => PreviewDistance.Near,
                _ => PreviewDistance.Near
            };

            RefreshHUD();
        }

        public void DoSaveExport()
        {
            if (exporter == null)
            {
                Debug.LogWarning("[ControllerMenu] DoSaveExport skipped: exporter is null.");
                return;
            }

            exporter.ExportAndSync();
        }

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
        }

        public void PreviewGltf()
        {
            if (exporter == null)
            {
                Debug.LogWarning("[ControllerMenu] PreviewGltf skipped: exporter is null.");
                return;
            }

            exporter.Export();
        }

        public void SetDisplayMode(AnchorDisplayMode mode)
        {
            _displayMode = mode;
            ApplyDisplayMode();
            RefreshHUD();
        }

        public float GetPreviewDistanceMetres()
        {
            return _previewDist switch
            {
                PreviewDistance.Near => 1f,
                PreviewDistance.Far => 3f,
                PreviewDistance.Off => 0f,
                _ => 1f
            };
        }

        private void ApplyModelPreset()
        {
            if (pipeline == null)
            {
                return;
            }

            switch (_activeModel)
            {
                case ModelPreset.Florence:
                    pipeline.RunFlorence = true;
                    pipeline.RunGroundingDino = false;
                    pipeline.RunSam2 = false;
                    break;
                case ModelPreset.GDino:
                    pipeline.RunFlorence = false;
                    pipeline.RunGroundingDino = true;
                    pipeline.RunSam2 = false;
                    break;
                case ModelPreset.SAM2:
                    pipeline.RunFlorence = false;
                    pipeline.RunGroundingDino = false;
                    pipeline.RunSam2 = true;
                    break;
                default:
                    pipeline.RunFlorence = true;
                    pipeline.RunGroundingDino = false;
                    pipeline.RunSam2 = false;
                    break;
            }
        }

        private void ApplyDisplayMode()
        {
            bool show2D = _displayMode == AnchorDisplayMode.BoundingBox2D || _displayMode == AnchorDisplayMode.Both;
            bool show3D = _displayMode == AnchorDisplayMode.SpatialLabel3D || _displayMode == AnchorDisplayMode.Both;

            if (bboxRenderer != null) bboxRenderer.gameObject.SetActive(show2D);
            if (labelRenderer != null) labelRenderer.gameObject.SetActive(show3D);

            var pm = pinManager != null ? pinManager : pipeline?.PinManager;
            if (pm != null)
            {
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
            if (pipeline == null || pipeline.Raycaster == null)
            {
                return;
            }

            float metres = GetPreviewDistanceMetres();
            if (metres > 0f)
            {
                pipeline.Raycaster.FallbackDistance = metres;
            }
        }

        private void RefreshHUD()
        {
            EnsureHudLabels();

            if (modelLabel != null)
            {
                modelLabel.text = _activeModel switch
                {
                    ModelPreset.Florence => "Model: Florence",
                    ModelPreset.GDino => "Model: DINO",
                    ModelPreset.SAM2 => "Model: SAM2",
                    _ => "Model: ?"
                };
            }

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

            if (previewDistanceLabel != null)
            {
                previewDistanceLabel.text = _previewDist switch
                {
                    PreviewDistance.Near => "Preview: Near (1m)",
                    PreviewDistance.Far => "Preview: Far (3m)",
                    PreviewDistance.Off => "Preview: Off",
                    _ => "Preview: ?"
                };
            }

            if (statusLabel != null)
            {
                statusLabel.text = BuildStatusText();
            }
        }

        private string BuildStatusText()
        {
            if (pipeline == null)
            {
                return "Raycast: pipeline missing";
            }

            string intrinsics = pipeline.LastCaptureHadIntrinsics ? "Intrinsics" : "PoseOnly";
            string fallback = pipeline.LastFallbackHitCount > 0
                ? $"Fallback: {pipeline.LastFallbackHitCount}"
                : "Fallback: 0";
            var config = pipeline.ServerConfig;
            string net = config != null
                ? $"Net: {Mathf.Max(1, config.requestTimeoutSeconds)}s x{Mathf.Max(0, config.retryCount)}"
                : "Net: ?";
            return $"Raycast: {intrinsics} | {fallback} | {net}";
        }

        private void EnsureHudLabels()
        {
            if (modelLabel == null) modelLabel = FindHudLabel("ModelLabel");
            if (displayModeLabel == null) displayModeLabel = FindHudLabel("DisplayLabel");
            if (previewDistanceLabel == null) previewDistanceLabel = FindHudLabel("PreviewLabel");
            if (busyLabel == null) busyLabel = FindHudLabel("BusyLabel");
            if (statusLabel == null) statusLabel = FindHudLabel("StatusLabel");
        }

        private void LogUiHealth()
        {
            var health = GetUiHealthStatus();
            if (!health.IsHealthy)
            {
                Debug.LogWarning(
                    $"[ControllerMenu] UI health degraded: pipeline={health.HasPipeline}, " +
                    $"labels={health.HasHudLabels}, input={health.HasInputBinding}, missingLabels={health.MissingHudLabels}");
            }
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