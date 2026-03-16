using System.Collections;
using CompanionScan.Export;
using CompanionScan.Mapping;
using CompanionScan.Rendering;
using TMPro;
using UnityEngine;

namespace CompanionScan.UI
{
    public class ControllerMenuBuilder : MonoBehaviour
    {
        [Header("Controller Anchors")]
        [SerializeField] private Transform rightAnchor;
        [SerializeField] private Transform leftAnchor;

        [Header("HUD Appearance")]
        [SerializeField] private float hudScale = 0.0004f;
        [SerializeField] private Vector3 hudOffset = new Vector3(0f, 0.05f, 0.08f);
        [SerializeField] private Vector3 hudRotation = new Vector3(30f, 0f, 0f);
        [SerializeField] private int fontSize = 24;
        [SerializeField] private Color bgColor = new Color(0.05f, 0.05f, 0.1f, 0.85f);
        [SerializeField] private Color textColor = Color.white;

        [Header("Button Indicator Appearance")]
        [SerializeField] private float circleRadius = 0.006f;
        [SerializeField] private float labelFontSize = 1.2f;
        [SerializeField] private float labelOffset = 0.003f;
        [SerializeField] private float labelSideOffset = 0.008f;

        private ControllerMenuUI _menuUI;
        private XRControllerInput _input;
        private bool _built;

        public ControllerMenuUI MenuUI => _menuUI;

        private static readonly (string name, Color color, string label, Vector3 pos)[] RightButtons =
        {
            ("A_Circle", new Color(0.9f, 0.15f, 0.15f, 1f), "CAPTURE", new Vector3(0.009f, 0.003f, -0.020f)),
            ("B_Circle", new Color(0.15f, 0.35f, 0.9f, 1f), "MODEL", new Vector3(0.009f, 0.003f, -0.035f))
        };

        private static readonly (string name, Color color, string label, Vector3 pos)[] LeftButtons =
        {
            ("X_Circle", new Color(0.95f, 0.85f, 0.1f, 1f), "PREVIEW", new Vector3(-0.009f, 0.003f, -0.020f)),
            ("Y_Circle", new Color(0.1f, 0.85f, 0.25f, 1f), "SAVE+EXPORT", new Vector3(-0.009f, 0.003f, -0.035f))
        };

        private static readonly Color TriggerColor = new Color(1f, 0.55f, 0.1f, 1f);

        private void Start()
        {
            if (rightAnchor == null) rightAnchor = FindAnchorRobust("Right");
            if (leftAnchor == null) leftAnchor = FindAnchorRobust("Left");

            BuildAll();

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
                    changed = changed || rightAnchor != null;
                }

                if (leftAnchor == null)
                {
                    leftAnchor = FindAnchorRobust("Left");
                    changed = changed || leftAnchor != null;
                }

                if (changed)
                {
                    RebuildIndicators();
                }

                if (rightAnchor != null && leftAnchor != null)
                {
                    yield break;
                }
            }

            if (rightAnchor == null || leftAnchor == null)
                Debug.LogWarning("[MenuBuilder] Controller anchors not found after retries.");
        }

        private void BuildAll()
        {
            if (_built)
            {
                RebuildIndicators();
                return;
            }

            var existingMenu = FindAnyObjectByType<ControllerMenuUI>();
            var rootGO = existingMenu != null ? existingMenu.gameObject : new GameObject("ControllerMenuRoot");
            if (existingMenu == null)
            {
                rootGO.transform.SetParent(transform, false);
                _menuUI = rootGO.AddComponent<ControllerMenuUI>();
            }
            else
            {
                _menuUI = existingMenu;
            }

            var pipeline = FindAnyObjectByType<SpatialCapturePipeline>();
            var exporter = FindAnyObjectByType<SpatialMapExporter>();
            var bboxR = FindAnyObjectByType<BBoxRenderer>();
            var maskR = FindAnyObjectByType<MaskRenderer>();
            var labelR = FindAnyObjectByType<LabelRenderer>();
            var captionP = FindAnyObjectByType<CaptionPanel>();
            var pinMgr = FindAnyObjectByType<SpatialPinManager>();
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

            if (frustumViz != null)
            {
                var targetCam = pipeline != null ? pipeline.SnapshotCamera : Camera.main;
                frustumViz.SetTargetCamera(targetCam);
            }

            SetField(_menuUI, "pipeline", pipeline);
            SetField(_menuUI, "exporter", exporter);
            SetField(_menuUI, "bboxRenderer", bboxR);
            SetField(_menuUI, "maskRenderer", maskR);
            SetField(_menuUI, "labelRenderer", labelR);
            SetField(_menuUI, "captionPanel", captionP);
            SetField(_menuUI, "pinManager", pinMgr);
            SetField(_menuUI, "frustumVisualizer", frustumViz);

            if (bboxR != null)
            {
                var previewAnchor = leftAnchor != null ? leftAnchor : Camera.main?.transform;
                SetField(bboxR, "previewAnchor", previewAnchor);
            }

            if (pipeline != null)
                SetField(pipeline, "snapshotStore", snapStore);
            if (exporter != null)
                SetField(exporter, "snapshotStore", snapStore);

            BuildHUD();
            RebuildIndicators();

            _input = FindAnyObjectByType<XRControllerInput>();
            if (_input == null)
                _input = rootGO.AddComponent<XRControllerInput>();
            SetField(_input, "menuUI", _menuUI);

            _built = true;
        }

        private void BuildHUD()
        {
            Transform parent = leftAnchor != null ? leftAnchor : transform;
            Transform existing = parent.Find("ControllerHUD");
            GameObject canvasGo;
            if (existing != null)
            {
                canvasGo = existing.gameObject;
            }
            else
            {
                canvasGo = new GameObject("ControllerHUD");
                canvasGo.transform.SetParent(parent, false);
            }

            canvasGo.transform.localPosition = hudOffset;
            canvasGo.transform.localRotation = Quaternion.Euler(hudRotation);
            canvasGo.transform.localScale = Vector3.one * hudScale;

            var canvas = canvasGo.GetComponent<Canvas>() ?? canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>() ?? canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            if (canvasGo.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(260f, 170f);

            var panel = canvasGo.transform.Find("HUDBg")?.gameObject;
            if (panel == null)
            {
                panel = new GameObject("HUDBg");
                panel.transform.SetParent(canvasGo.transform, false);
                var panelRT = panel.AddComponent<RectTransform>();
                panelRT.anchorMin = Vector2.zero;
                panelRT.anchorMax = Vector2.one;
                panelRT.offsetMin = Vector2.zero;
                panelRT.offsetMax = Vector2.zero;
                panel.AddComponent<UnityEngine.UI.Image>();
            }
            panel.GetComponent<UnityEngine.UI.Image>().color = bgColor;

            var layout = panel.transform.Find("Layout")?.gameObject;
            if (layout == null)
            {
                layout = new GameObject("Layout");
                layout.transform.SetParent(panel.transform, false);
                var layoutRT = layout.AddComponent<RectTransform>();
                layoutRT.anchorMin = Vector2.zero;
                layoutRT.anchorMax = Vector2.one;
                layoutRT.offsetMin = new Vector2(8, 8);
                layoutRT.offsetMax = new Vector2(-8, -8);
                var vlg = layout.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                vlg.spacing = 4f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
            }

            var modelLbl = EnsureLabel(layout.transform, "ModelLabel", "Model: All", 260f, 26f);
            var displayLbl = EnsureLabel(layout.transform, "DisplayLabel", "Display: Both", 260f, 26f);
            var previewLbl = EnsureLabel(layout.transform, "PreviewLabel", "Preview: Near (1m)", 260f, 26f);
            var statusLbl = EnsureLabel(layout.transform, "StatusLabel", "Raycast: --", 260f, 26f);
            var busyLbl = EnsureLabel(layout.transform, "BusyLabel", string.Empty, 260f, 26f);
            busyLbl.color = Color.yellow;

            SetField(_menuUI, "modelLabel", modelLbl);
            SetField(_menuUI, "displayModeLabel", displayLbl);
            SetField(_menuUI, "previewDistanceLabel", previewLbl);
            SetField(_menuUI, "statusLabel", statusLbl);
            SetField(_menuUI, "busyLabel", busyLbl);
        }

        private TMP_Text EnsureLabel(Transform parent, string name, string text, float width, float height)
        {
            var existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null) go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);

            var tmp = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private void RebuildIndicators()
        {
            if (rightAnchor != null)
            {
                foreach (var b in RightButtons)
                    SpawnIndicator(rightAnchor, b.name, b.color, b.label, b.pos, true);
                SpawnIndicator(rightAnchor, "RTrigger_Circle", TriggerColor, "DISPLAY", new Vector3(0f, -0.005f, 0.02f), true);
            }

            if (leftAnchor != null)
            {
                foreach (var b in LeftButtons)
                    SpawnIndicator(leftAnchor, b.name, b.color, b.label, b.pos, false);
                SpawnIndicator(leftAnchor, "LTrigger_Circle", TriggerColor, "DISPLAY", new Vector3(0f, -0.005f, 0.02f), false);
            }

            if (leftAnchor != null)
            {
                var bbox = FindAnyObjectByType<BBoxRenderer>();
                if (bbox != null)
                    SetField(bbox, "previewAnchor", leftAnchor);

                var hud = transform.Find("ControllerHUD") ?? transform.GetComponentInChildren<Canvas>()?.transform;
                if (hud != null && hud.parent != leftAnchor)
                {
                    hud.SetParent(leftAnchor, false);
                    hud.localPosition = hudOffset;
                    hud.localRotation = Quaternion.Euler(hudRotation);
                    hud.localScale = Vector3.one * hudScale;
                }
            }
        }

        private void SpawnIndicator(Transform anchor, string goName, Color color, string label, Vector3 localPos, bool isRightController)
        {
            var existing = anchor.Find(goName);
            var root = existing != null ? existing.gameObject : new GameObject(goName);
            if (existing == null)
                root.transform.SetParent(anchor, false);

            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.identity;

            Transform circleTransform = root.transform.Find("Circle");
            GameObject circleGO;
            if (circleTransform == null)
            {
                circleGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                circleGO.name = "Circle";
                circleGO.transform.SetParent(root.transform, false);
                var col = circleGO.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
            else
            {
                circleGO = circleTransform.gameObject;
            }

            circleGO.transform.localScale = Vector3.one * (circleRadius * 2f);
            circleGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var renderer = circleGO.GetComponent<MeshRenderer>();
            if (renderer.sharedMaterial == null)
                renderer.sharedMaterial = new Material(Shader.Find("UI/Default"));
            renderer.sharedMaterial.color = color;

            Transform labelTransform = root.transform.Find("Label");
            GameObject labelGO = labelTransform != null ? labelTransform.gameObject : new GameObject("Label");
            if (labelTransform == null)
                labelGO.transform.SetParent(root.transform, false);

            float sideSign = isRightController ? -1f : 1f;
            labelGO.transform.localPosition = new Vector3(sideSign * labelSideOffset, labelOffset, 0f);
            labelGO.transform.localRotation = Quaternion.Euler(0f, sideSign * 90f, 0f);

            var tmp = labelGO.GetComponent<TextMeshPro>() ?? labelGO.AddComponent<TextMeshPro>();
            tmp.text = label;
            tmp.fontSize = labelFontSize;
            tmp.alignment = isRightController ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            var tmpRT = tmp.GetComponent<RectTransform>();
            tmpRT.sizeDelta = new Vector2(0.02f, 0.006f);
            tmpRT.pivot = isRightController ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
        }

        private static Transform FindAnchorRobust(string side)
        {
            string[] exactNames = side == "Right"
                ? new[] { "RightControllerAnchor", "RightHandAnchor", "RightControllerInHandAnchor", "RightInteractorAnchor" }
                : new[] { "LeftControllerAnchor", "LeftHandAnchor", "LeftControllerInHandAnchor", "LeftInteractorAnchor" };

            foreach (var name in exactNames)
            {
                var go = GameObject.Find(name);
                if (go != null)
                    return go.transform;
            }

            string sideLower = side.ToLowerInvariant();
            Transform fallback = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string nLower = t.name.ToLowerInvariant();
                if (!nLower.Contains(sideLower))
                    continue;
                if (nLower.Contains("controller") || nLower.Contains("hand"))
                {
                    if (nLower.Contains("anchor"))
                        return t;
                    fallback ??= t;
                }
            }

            if (fallback != null)
                return fallback;

            var cameraRig = FindAnyObjectByType<OVRCameraRig>();
            if (cameraRig != null)
            {
                return side == "Right"
                    ? cameraRig.rightControllerAnchor ?? cameraRig.rightHandAnchor
                    : cameraRig.leftControllerAnchor ?? cameraRig.leftHandAnchor;
            }

            return null;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (target == null || value == null)
                return;

            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}