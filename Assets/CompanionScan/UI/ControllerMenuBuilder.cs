using System.Collections;
using CompanionScan.Export;
using CompanionScan.Mapping;
using CompanionScan.Rendering;
using TMPro;
using UnityEngine;

namespace CompanionScan.UI
{
    /// <summary>
    /// Builds the XR-controller-driven UI at runtime:
    ///   1. A small HUD Canvas on the left wrist showing Model / Display / Preview status.
    ///   2. Coloured circle sprites + labels near each physical Quest controller button.
    ///   3. Wires <see cref="XRControllerInput"/> â†’ <see cref="ControllerMenuUI"/>.
    ///
    /// Drop this on any GameObject in the scene.  Everything is created in Awake().
    /// </summary>
    public class ControllerMenuBuilder : MonoBehaviour
    {
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Config
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Header("Controller Anchors")]
        [Tooltip("Right controller transform (button indicators). Auto-found if null.")]
        [SerializeField] private Transform rightAnchor;

        [Tooltip("Left controller transform (HUD panel). Auto-found if null.")]
        [SerializeField] private Transform leftAnchor;

        [Header("HUD Appearance")]
        [SerializeField] private float hudScale = 0.0004f;
        [SerializeField] private Vector3 hudOffset = new(0f, 0.05f, 0.08f);
        [SerializeField] private Vector3 hudRotation = new(30f, 0f, 0f);
        [SerializeField] private int fontSize = 24;
        [SerializeField] private Color bgColor = new(0.05f, 0.05f, 0.1f, 0.85f);
        [SerializeField] private Color textColor = Color.white;

        [Header("Button Indicator Appearance")]
        [SerializeField] private float circleRadius = 0.006f;
        [SerializeField] private float labelFontSize = 14f;
        [SerializeField] private float labelOffset = 0.012f;

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Built references
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private ControllerMenuUI _menuUI;
        private XRControllerInput _input;

        public ControllerMenuUI MenuUI => _menuUI;

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Button layout â€“ local-space positions relative to controller anchor
        //  Values approximate Meta Quest Touch Pro / Touch Plus geometry.
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static readonly (string name, Color color, string label, Vector3 pos)[] RightButtons =
        {
            ("A_Circle", new Color(0.9f, 0.15f, 0.15f, 1f),  "CAPTURE",     new Vector3( 0.009f, 0.003f, -0.020f)),
            ("B_Circle", new Color(0.15f, 0.35f, 0.9f, 1f), "MODEL",       new Vector3( 0.009f, 0.003f, -0.035f)),
        };

        private static readonly (string name, Color color, string label, Vector3 pos)[] LeftButtons =
        {
            ("X_Circle", new Color(0.95f, 0.85f, 0.1f, 1f),  "PREVIEW",     new Vector3(-0.009f, 0.003f, -0.020f)),
            ("Y_Circle", new Color(0.1f, 0.85f, 0.25f, 1f),  "SAVE+EXPORT", new Vector3(-0.009f, 0.003f, -0.035f)),
        };

        // Trigger indicators (shared color â€” orange)
        private static readonly Color TriggerColor = new(1f, 0.55f, 0.1f, 1f);

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Awake()
        {
            // Defer to Start so OVR rig hierarchy is initialised before we search
        }

        private void Start()
        {
            if (rightAnchor == null) rightAnchor = FindAnchorRobust("Right");
            if (leftAnchor  == null) leftAnchor  = FindAnchorRobust("Left");

            BuildAll();

            // If anchors weren't found yet (rig may initialise late), retry
            if (rightAnchor == null || leftAnchor == null)
                StartCoroutine(RetryAnchorSearch());
        }

        private IEnumerator RetryAnchorSearch()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                yield return new WaitForSeconds(0.5f);
                bool changed = false;
                if (rightAnchor == null)
                {
                    rightAnchor = FindAnchorRobust("Right");
                    if (rightAnchor != null) changed = true;
                }
                if (leftAnchor == null)
                {
                    leftAnchor = FindAnchorRobust("Left");
                    if (leftAnchor != null) changed = true;
                }
                if (changed)
                {
                    Debug.Log($"[MenuBuilder] Late anchor discovery: R={rightAnchor?.name} L={leftAnchor?.name}");
                    RebuildIndicators();
                }
                if (rightAnchor != null && leftAnchor != null) yield break;
            }
            if (rightAnchor == null || leftAnchor == null)
                Debug.LogWarning("[MenuBuilder] Controller anchors not found after retries. " +
                                 "Indicators will be missing but input still works.");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Build
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void BuildAll()
        {
            // 1. Create root GO that holds ControllerMenuUI
            var rootGO = new GameObject("ControllerMenuRoot");
            rootGO.transform.SetParent(transform, false);
            _menuUI = rootGO.AddComponent<ControllerMenuUI>();

            // 2. Auto-discover scene singletons
            var pipeline  = FindAnyObjectByType<SpatialCapturePipeline>();
            var exporter  = FindAnyObjectByType<SpatialMapExporter>();
            var bboxR     = FindAnyObjectByType<BBoxRenderer>();
            var maskR     = FindAnyObjectByType<MaskRenderer>();
            var labelR    = FindAnyObjectByType<LabelRenderer>();
            var captionP  = FindAnyObjectByType<CaptionPanel>();
            var pinMgr    = FindAnyObjectByType<SpatialPinManager>();
            var snapStore = FindAnyObjectByType<SnapshotFrameStore>();
            var frustumViz = FindAnyObjectByType<SnapshotFrustumVisualizer>();
            if (snapStore == null)
            {
                var snapGO = new GameObject("SnapshotFrameStore");
                snapGO.transform.SetParent(rootGO.transform, false);
                snapStore = snapGO.AddComponent<SnapshotFrameStore>();
            }
            if (frustumViz == null)
            {
                var frustumGO = new GameObject("SnapshotFrustumVisualizer");
                frustumGO.transform.SetParent(rootGO.transform, false);
                frustumViz = frustumGO.AddComponent<SnapshotFrustumVisualizer>();
            }

            SetField(_menuUI, "pipeline",     pipeline);
            SetField(_menuUI, "exporter",     exporter);
            SetField(_menuUI, "bboxRenderer", bboxR);
            SetField(_menuUI, "maskRenderer", maskR);
            SetField(_menuUI, "labelRenderer",labelR);
            SetField(_menuUI, "captionPanel", captionP);
            SetField(_menuUI, "pinManager",   pinMgr);
            SetField(_menuUI, "frustumVisualizer", frustumViz);

            if (pipeline != null)
            {
                SetField(pipeline, "snapshotStore", snapStore);
            }
            if (exporter != null)
            {
                SetField(exporter, "snapshotStore", snapStore);
            }

            // 3. HUD panel on wrist
            BuildHUD();

            // 4. Coloured button indicators on controllers
            if (rightAnchor != null)
            {
                foreach (var b in RightButtons)
                    SpawnIndicator(rightAnchor, b.name, b.color, b.label, b.pos);
                SpawnIndicator(rightAnchor, "RTrigger_Circle", TriggerColor, "DISPLAY",
                    new Vector3(0f, -0.005f, 0.02f));
            }

            if (leftAnchor != null)
            {
                foreach (var b in LeftButtons)
                    SpawnIndicator(leftAnchor, b.name, b.color, b.label, b.pos);
                SpawnIndicator(leftAnchor, "LTrigger_Circle", TriggerColor, "DISPLAY",
                    new Vector3(0f, -0.005f, 0.02f));
            }

            // 5. Wire XRControllerInput
            _input = rootGO.AddComponent<XRControllerInput>();
            SetField(_input, "menuUI", _menuUI);

            if (pipeline == null)
                Debug.LogWarning("[MenuBuilder] SpatialCapturePipeline not found in scene!");
            if (exporter == null)
                Debug.LogWarning("[MenuBuilder] SpatialMapExporter not found.");
            if (pinMgr == null)
                Debug.LogWarning("[MenuBuilder] SpatialPinManager not found.");

            Debug.Log($"[MenuBuilder] Controller menu built. pipeline={(pipeline != null ? "OK" : "MISSING")} " +
                      $"R={rightAnchor?.name ?? "null"} L={leftAnchor?.name ?? "null"}");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  HUD â€” small world-space status panel on left wrist
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void BuildHUD()
        {
            Transform parent = leftAnchor != null ? leftAnchor : transform;
            var canvasGO = new GameObject("ControllerHUD");
            canvasGO.transform.SetParent(parent, false);
            canvasGO.transform.localPosition = hudOffset;
            canvasGO.transform.localRotation = Quaternion.Euler(hudRotation);
            canvasGO.transform.localScale    = Vector3.one * hudScale;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            float panelW = 260f, panelH = 140f;
            canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(panelW, panelH);

            // Background
            var panelGO = new GameObject("HUDBg");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            panelGO.AddComponent<UnityEngine.UI.Image>().color = bgColor;

            // Layout
            var layoutGO = new GameObject("Layout");
            layoutGO.transform.SetParent(panelGO.transform, false);
            var layoutRT = layoutGO.AddComponent<RectTransform>();
            layoutRT.anchorMin = Vector2.zero;
            layoutRT.anchorMax = Vector2.one;
            layoutRT.offsetMin = new Vector2(8, 8);
            layoutRT.offsetMax = new Vector2(-8, -8);
            var vlg = layoutGO.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Labels
            var modelLbl   = MakeLabel(layoutGO.transform, "ModelLabel",   "Model: All",        260f, 26f);
            var displayLbl = MakeLabel(layoutGO.transform, "DisplayLabel", "Display: Both",     260f, 26f);
            var previewLbl = MakeLabel(layoutGO.transform, "PreviewLabel", "Preview: Near (1m)",260f, 26f);
            var busyLbl    = MakeLabel(layoutGO.transform, "BusyLabel",    "",                  260f, 26f);
            busyLbl.color = Color.yellow;

            // Inject into MenuUI
            SetField(_menuUI, "modelLabel",           modelLbl);
            SetField(_menuUI, "displayModeLabel",     displayLbl);
            SetField(_menuUI, "previewDistanceLabel", previewLbl);
            SetField(_menuUI, "busyLabel",            busyLbl);
        }

        private TMP_Text MakeLabel(Transform parent, string name, string text,
                                   float width, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Button indicators â€” coloured circle + label on controller
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void SpawnIndicator(Transform anchor, string goName, Color color,
                                    string label, Vector3 localPos)
        {
            // Root
            var root = new GameObject(goName);
            root.transform.SetParent(anchor, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.identity;

            // Circle quad
            var circleGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            circleGO.name = "Circle";
            circleGO.transform.SetParent(root.transform, false);
            circleGO.transform.localScale = Vector3.one * (circleRadius * 2f);
            circleGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // face up

            // Destroy collider from primitive
            var col = circleGO.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Material â€” unlit, transparent, circle shape via shader
            var mat = new Material(Shader.Find("UI/Default"));
            mat.color = color;
            circleGO.GetComponent<MeshRenderer>().material = mat;

            // Label (world-space TMP)
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(root.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, labelOffset, 0f);
            labelGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var tmp = labelGO.AddComponent<TextMeshPro>();
            tmp.text = label;
            tmp.fontSize = labelFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            var tmpRT = tmp.GetComponent<RectTransform>();
            tmpRT.sizeDelta = new Vector2(0.06f, 0.02f);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Utilities
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        // =================================================================
        //  Rebuild indicators when anchors are discovered late
        // =================================================================

        private void RebuildIndicators()
        {
            if (rightAnchor != null)
            {
                foreach (var b in RightButtons)
                    SpawnIndicator(rightAnchor, b.name, b.color, b.label, b.pos);
                SpawnIndicator(rightAnchor, "RTrigger_Circle", TriggerColor, "DISPLAY",
                    new Vector3(0f, -0.005f, 0.02f));
            }
            if (leftAnchor != null)
            {
                foreach (var b in LeftButtons)
                    SpawnIndicator(leftAnchor, b.name, b.color, b.label, b.pos);
                SpawnIndicator(leftAnchor, "LTrigger_Circle", TriggerColor, "DISPLAY",
                    new Vector3(0f, -0.005f, 0.02f));
            }
            // Re-parent HUD to left wrist if it was on fallback transform
            var hud = transform.Find("ControllerMenuRoot")?.Find("ControllerHUD") ??
                      transform.GetComponentInChildren<Canvas>()?.transform;
            if (hud != null && leftAnchor != null && hud.parent != leftAnchor)
            {
                hud.SetParent(leftAnchor, false);
                hud.localPosition = hudOffset;
                hud.localRotation = Quaternion.Euler(hudRotation);
                hud.localScale    = Vector3.one * hudScale;
            }
        }

        // =================================================================
        //  Anchor search â€” robust multi-strategy
        // =================================================================

        /// <summary>
        /// Find right or left controller anchor using multiple strategies:
        /// 1. Exact GameObject.Find for known OVR names
        /// 2. Partial name search across all transforms
        /// 3. OVRCameraRig child hierarchy
        /// </summary>
        private static Transform FindAnchorRobust(string side)
        {
            // Strategy 1: Exact names used across Meta XR SDK versions
            string[] exactNames = side == "Right"
                ? new[] { "RightControllerAnchor", "RightHandAnchor",
                          "RightControllerInHandAnchor", "RightInteractorAnchor" }
                : new[] { "LeftControllerAnchor", "LeftHandAnchor",
                          "LeftControllerInHandAnchor", "LeftInteractorAnchor" };

            foreach (var name in exactNames)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    Debug.Log($"[MenuBuilder] Found {side} anchor by exact name: {go.name}");
                    return go.transform;
                }
            }

            // Strategy 2: Search all transforms for partial match
            string controllerKey = side + "Controller";
            string handKey = side + "Hand";
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = t.name;
                if (n.Contains(controllerKey) || n.Contains(handKey))
                {
                    // Prefer "Anchor" suffix, but take any match
                    if (n.Contains("Anchor"))
                    {
                        Debug.Log($"[MenuBuilder] Found {side} anchor by search: {n}");
                        return t;
                    }
                }
            }

            // Strategy 3: OVRCameraRig hierarchy
            var cameraRig = FindAnyObjectByType<OVRCameraRig>();
            if (cameraRig != null)
            {
                Transform anchor = side == "Right"
                    ? cameraRig.rightControllerAnchor ?? cameraRig.rightHandAnchor
                    : cameraRig.leftControllerAnchor  ?? cameraRig.leftHandAnchor;
                if (anchor != null)
                {
                    Debug.Log($"[MenuBuilder] Found {side} anchor via OVRCameraRig: {anchor.name}");
                    return anchor;
                }
            }

            Debug.LogWarning($"[MenuBuilder] {side} controller anchor not found (will retry).");
            return null;
        }

        // Keep legacy method signature for backward compatibility
        private static Transform FindAnchor(params string[] names)
        {
            foreach (var n in names)
            {
                var go = GameObject.Find(n);
                if (go != null) return go.transform;
            }
            return null;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (value == null) return;
            var field = target.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.NonPublic |
                                     System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
            else
                Debug.LogWarning($"[MenuBuilder] Field '{fieldName}' not found on " +
                                 $"{target.GetType().Name}.");
        }
    }
}

