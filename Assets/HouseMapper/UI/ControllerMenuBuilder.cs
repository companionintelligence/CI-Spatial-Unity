using HouseMapper.Export;
using HouseMapper.Mapping;
using HouseMapper.Rendering;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HouseMapper.UI
{
    /// <summary>
    /// Programmatically spawns a world-space controller menu panel as a child
    /// of the left-hand controller anchor.  Finds pipeline / exporter / renderer
    /// references in the scene automatically.
    ///
    /// Usage:  Put this component on any GameObject in the scene.  At Awake() it
    /// builds the Canvas, buttons, and wires everything up – no manual prefab
    /// setup needed.
    /// </summary>
    public class ControllerMenuBuilder : MonoBehaviour
    {
        [Header("Controller Anchor")]
        [Tooltip("Left controller transform to parent the menu to. " +
                 "If null, searches for OVRControllerHelper or " +
                 "LeftControllerAnchor in the scene.")]
        [SerializeField] private Transform controllerAnchor;

        [Header("Appearance")]
        [SerializeField] private float canvasScale = 0.0004f;
        [SerializeField] private Vector3 localOffset = new(0f, 0.05f, 0.08f);
        [SerializeField] private Vector3 localRotation = new(30f, 0f, 0f);
        [SerializeField] private float buttonWidth = 260f;
        [SerializeField] private float buttonHeight = 50f;
        [SerializeField] private float spacing = 6f;
        [SerializeField] private int fontSize = 28;
        [SerializeField] private Color bgColor = new(0.05f, 0.05f, 0.1f, 0.85f);
        [SerializeField] private Color btnColor = new(0.15f, 0.15f, 0.25f, 0.9f);
        [SerializeField] private Color textColor = Color.white;

        private ControllerMenuUI _menuUI;

        /// <summary>The built menu UI, available after Awake.</summary>
        public ControllerMenuUI MenuUI => _menuUI;

        private void Awake()
        {
            BuildMenu();
        }

        // ═══════════════════════════════════════════════════════════════
        //  Menu construction
        // ═══════════════════════════════════════════════════════════════

        private void BuildMenu()
        {
            // --- Find controller anchor ---
            if (controllerAnchor == null)
            {
                controllerAnchor = FindLeftControllerAnchor();
            }

            Transform parent = controllerAnchor != null ? controllerAnchor : transform;

            // --- Canvas ---
            var canvasGO = new GameObject("ControllerMenuCanvas");
            canvasGO.transform.SetParent(parent, false);
            canvasGO.transform.localPosition = localOffset;
            canvasGO.transform.localRotation = Quaternion.Euler(localRotation);
            canvasGO.transform.localScale = Vector3.one * canvasScale;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Get canvas RectTransform
            var canvasRT = canvasGO.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(buttonWidth + 20f, 460f);

            // --- Background panel ---
            var panelGO = CreatePanel(canvasGO.transform, "Panel", canvasRT.sizeDelta);

            // --- Vertical layout ---
            var layoutGO = new GameObject("Layout");
            layoutGO.transform.SetParent(panelGO.transform, false);
            var layoutRT = layoutGO.AddComponent<RectTransform>();
            layoutRT.anchorMin = Vector2.zero;
            layoutRT.anchorMax = Vector2.one;
            layoutRT.offsetMin = new Vector2(8, 8);
            layoutRT.offsetMax = new Vector2(-8, -8);

            var vlg = layoutGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // --- Create buttons ---
            var captureAllBtn = CreateButton(layoutGO.transform, "Capture All", buttonHeight);
            var captureCvBtn = CreateButton(layoutGO.transform, "Capture CV", buttonHeight);
            var captureMetaBtn = CreateButton(layoutGO.transform, "Capture Meta", buttonHeight);

            // Toggle buttons with status labels
            var (florenceBtn, florenceLbl) = CreateToggleRow(layoutGO.transform, "Florence2: ON", buttonHeight);
            var (dinoBtn, dinoLbl) = CreateToggleRow(layoutGO.transform, "GDino: ON", buttonHeight);
            var (sam2Btn, sam2Lbl) = CreateToggleRow(layoutGO.transform, "SAM2: ON", buttonHeight);
            var (displayBtn, displayLbl) = CreateToggleRow(layoutGO.transform, "Display: Both", buttonHeight);

            var saveBtn = CreateButton(layoutGO.transform, "Save & Export", buttonHeight);

            // Busy label
            var busyGO = new GameObject("BusyLabel");
            busyGO.transform.SetParent(layoutGO.transform, false);
            var busyRT = busyGO.AddComponent<RectTransform>();
            busyRT.sizeDelta = new Vector2(buttonWidth, 30f);
            var busyTMP = busyGO.AddComponent<TextMeshProUGUI>();
            busyTMP.text = "";
            busyTMP.fontSize = fontSize - 4;
            busyTMP.alignment = TextAlignmentOptions.Center;
            busyTMP.color = Color.yellow;

            // --- Wire ControllerMenuUI ---
            _menuUI = canvasGO.AddComponent<ControllerMenuUI>();

            // Find scene references
            var pipeline = FindAnyObjectByType<SpatialCapturePipeline>();
            var exporter = FindAnyObjectByType<SpatialMapExporter>();
            var bboxR = FindAnyObjectByType<BBoxRenderer>();
            var maskR = FindAnyObjectByType<MaskRenderer>();
            var labelR = FindAnyObjectByType<LabelRenderer>();
            var captionP = FindAnyObjectByType<CaptionPanel>();

            // Use reflection-free serialized field injection via a helper
            SetPrivateField(_menuUI, "pipeline", pipeline);
            SetPrivateField(_menuUI, "exporter", exporter);
            SetPrivateField(_menuUI, "bboxRenderer", bboxR);
            SetPrivateField(_menuUI, "maskRenderer", maskR);
            SetPrivateField(_menuUI, "labelRenderer", labelR);
            SetPrivateField(_menuUI, "captionPanel", captionP);

            SetPrivateField(_menuUI, "captureAllButton", captureAllBtn);
            SetPrivateField(_menuUI, "captureCvButton", captureCvBtn);
            SetPrivateField(_menuUI, "captureMetaButton", captureMetaBtn);
            SetPrivateField(_menuUI, "saveExportButton", saveBtn);

            SetPrivateField(_menuUI, "toggleFlorenceButton", florenceBtn);
            SetPrivateField(_menuUI, "toggleDinoButton", dinoBtn);
            SetPrivateField(_menuUI, "toggleSam2Button", sam2Btn);
            SetPrivateField(_menuUI, "toggleDisplayModeButton", displayBtn);

            SetPrivateField(_menuUI, "florenceStatusLabel", florenceLbl);
            SetPrivateField(_menuUI, "dinoStatusLabel", dinoLbl);
            SetPrivateField(_menuUI, "sam2StatusLabel", sam2Lbl);
            SetPrivateField(_menuUI, "displayModeLabel", displayLbl);
            SetPrivateField(_menuUI, "busyLabel", busyTMP);

            if (pipeline == null)
                Debug.LogWarning("[MenuBuilder] SpatialCapturePipeline not found in scene.");
            if (exporter == null)
                Debug.LogWarning("[MenuBuilder] SpatialMapExporter not found in scene.");

            Debug.Log("[MenuBuilder] Controller menu built successfully.");
        }

        // ═══════════════════════════════════════════════════════════════
        //  UI factory helpers
        // ═══════════════════════════════════════════════════════════════

        private GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = bgColor;
            return go;
        }

        private Button CreateButton(Transform parent, string label, float height)
        {
            var go = new GameObject($"Btn_{label.Replace(" ", "")}");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(buttonWidth, height);

            var img = go.AddComponent<Image>();
            img.color = btnColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colours = btn.colors;
            colours.normalColor = btnColor;
            colours.highlightedColor = new Color(0.25f, 0.25f, 0.4f, 0.95f);
            colours.pressedColor = new Color(0.35f, 0.35f, 0.55f, 1f);
            btn.colors = colours;

            // Label
            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(8, 0);
            txtRT.offsetMax = new Vector2(-8, 0);

            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.enableWordWrapping = false;

            return btn;
        }

        /// <summary>Creates a button whose label doubles as the status text.</summary>
        private (Button btn, TMP_Text label) CreateToggleRow(Transform parent,
            string initialText, float height)
        {
            var btn = CreateButton(parent, initialText, height);
            // The TMP_Text child is the status label
            var tmp = btn.GetComponentInChildren<TMP_Text>();
            return (btn, tmp);
        }

        // ═══════════════════════════════════════════════════════════════
        //  Utilities
        // ═══════════════════════════════════════════════════════════════

        private static Transform FindLeftControllerAnchor()
        {
            // Try OVR naming convention first
            var go = GameObject.Find("LeftControllerAnchor");
            if (go != null) return go.transform;

            // Try LeftHandAnchor
            go = GameObject.Find("LeftHandAnchor");
            if (go != null) return go.transform;

            Debug.LogWarning("[MenuBuilder] Could not find left controller anchor. " +
                             "Menu will be parented to this GameObject.");
            return null;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (value == null) return;
            var field = target.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.NonPublic |
                                     System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                Debug.LogWarning($"[MenuBuilder] Field '{fieldName}' not found on {target.GetType().Name}.");
            }
        }
    }
}
