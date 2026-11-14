using System.Collections.Generic;
using CompanionScan.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompanionScan.Rendering
{
    public class BBoxRenderer : MonoBehaviour
    {
        [SerializeField] private RectTransform container;
        [SerializeField] private RectTransform resultImage;
        [SerializeField] private GameObject bboxPrefab;
        [SerializeField] private Transform previewAnchor;
        [SerializeField] private Vector3 previewOffset = new Vector3(0f, -0.1f, 0.6f);
        [SerializeField] private Vector3 previewRotation = Vector3.zero;
        [SerializeField] private float previewScale = 0.0004f;

        private GameObject _runtimePrefab;
        private RawImage _runtimeImage;
        private bool _visible = true;

        private readonly List<GameObject> _instances = new List<GameObject>();

        /// <summary>
        /// Shows or hides the camera preview panel and its boxes. Only the panel's canvas is
        /// switched off, never this component's GameObject, which in the shipped scene also runs
        /// capture. Boxes are still drawn while the panel is hidden, so it shows the latest
        /// capture's boxes when it comes back.
        /// </summary>
        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                ApplyVisibility();
            }
        }

        /// <summary>The outermost canvas of the preview panel, once there is one.</summary>
        public Canvas PreviewCanvas
        {
            get
            {
                if (container == null)
                {
                    return null;
                }

                // Ordered from the container outwards; the last is the outermost.
                var canvases = container.GetComponentsInParent<Canvas>(true);
                return canvases.Length > 0 ? canvases[canvases.Length - 1] : null;
            }
        }

        private void ApplyVisibility()
        {
            var canvas = PreviewCanvas;
            if (canvas != null && canvas.enabled != _visible)
            {
                canvas.enabled = _visible;
            }
        }

        public void RenderBoxes(DetectionResult result, Texture sourceTexture)
        {
            Clear();
            EnsureDefaults(sourceTexture);
            if (sourceTexture == null || container == null || bboxPrefab == null)
            {
                return;
            }

            if (result == null || result.bboxes == null || result.labels == null)
            {
                return;
            }

            Rect containerRect = container.rect;

            for (int i = 0; i < result.bboxes.Count; i++)
            {
                var bbox = result.bboxes[i];
                if (bbox == null || bbox.Length < 4)
                {
                    continue;
                }

                if (!BoundingBoxUtils.TryNormalize(bbox, sourceTexture.width, sourceTexture.height, out Rect normalized))
                {
                    continue;
                }

                float x1 = normalized.x * containerRect.width;
                float y1 = normalized.y * containerRect.height;
                float x2 = (normalized.x + normalized.width) * containerRect.width;
                float y2 = (normalized.y + normalized.height) * containerRect.height;

                var boxObj = Instantiate(bboxPrefab, container);
                // The runtime template is kept inactive; its copies must not be.
                boxObj.SetActive(true);
                var rect = boxObj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));
                rect.anchoredPosition = new Vector2(x1, containerRect.height - y2);

                var label = boxObj.GetComponentInChildren<TMP_Text>(true);
                if (label != null && i < result.labels.Count)
                {
                    label.text = result.labels[i];
                }

                _instances.Add(boxObj);
            }
        }

        private void EnsureDefaults(Texture sourceTexture)
        {
            if (container == null)
            {
                container = FindContainer();
            }

            if (container == null)
            {
                CreatePreviewCanvas(sourceTexture, out RectTransform createdContainer, out RawImage createdImage);
                container = createdContainer;
                resultImage = createdImage != null ? createdImage.rectTransform : null;
                _runtimeImage = createdImage;
            }

            if (container != null && sourceTexture != null)
            {
                container.sizeDelta = new Vector2(sourceTexture.width, sourceTexture.height);
                if (_runtimeImage != null) _runtimeImage.texture = sourceTexture;
                if (resultImage != null)
                {
                    var rawImage = resultImage.GetComponent<RawImage>();
                    if (rawImage != null)
                    {
                        rawImage.texture = sourceTexture;
                    }
                }
            }

            if (bboxPrefab == null)
            {
                if (_runtimePrefab == null && container != null)
                {
                    _runtimePrefab = BuildRuntimePrefab(container);
                }
                bboxPrefab = _runtimePrefab;
            }

            // A panel made while the boxes are hidden starts hidden.
            ApplyVisibility();
        }

        public void SetPreviewTexture(Texture sourceTexture)
        {
            EnsureDefaults(sourceTexture);
            if (_runtimeImage != null && sourceTexture != null)
            {
                _runtimeImage.texture = sourceTexture;
            }
            else if (resultImage != null && sourceTexture != null)
            {
                var rawImage = resultImage.GetComponent<RawImage>();
                if (rawImage != null)
                {
                    rawImage.texture = sourceTexture;
                }
            }
        }

        private RectTransform FindContainer()
        {
            var containerGo = GameObject.Find("BBOX Container") ?? GameObject.Find("BBoxContainer");
            return containerGo != null ? containerGo.GetComponent<RectTransform>() : null;
        }

        private void CreatePreviewCanvas(Texture sourceTexture, out RectTransform createdContainer, out RawImage createdImage)
        {
            var canvasGo = new GameObject("CameraPreviewCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var anchor = previewAnchor != null ? previewAnchor : Camera.main?.transform;
            if (anchor != null)
            {
                canvasGo.transform.SetParent(anchor, false);
                canvasGo.transform.localPosition = previewOffset;
                canvasGo.transform.localRotation = Quaternion.Euler(previewRotation);
                canvasGo.transform.localScale = Vector3.one * previewScale;
            }
            else
            {
                canvasGo.transform.localScale = Vector3.one * previewScale;
            }

            var imageGo = new GameObject("ResultImage");
            imageGo.transform.SetParent(canvasGo.transform, false);
            createdImage = imageGo.AddComponent<RawImage>();
            if (sourceTexture != null)
            {
                createdImage.texture = sourceTexture;
            }

            var imageRt = imageGo.GetComponent<RectTransform>();
            imageRt.anchorMin = new Vector2(0.5f, 0.5f);
            imageRt.anchorMax = new Vector2(0.5f, 0.5f);
            imageRt.pivot = new Vector2(0.5f, 0.5f);
            float width = sourceTexture != null ? sourceTexture.width : 1280f;
            float height = sourceTexture != null ? sourceTexture.height : 960f;
            imageRt.sizeDelta = new Vector2(width, height);

            var containerGo = new GameObject("BBOX Container", typeof(RectTransform));
            containerGo.transform.SetParent(imageGo.transform, false);
            createdContainer = containerGo.GetComponent<RectTransform>();
            createdContainer.anchorMin = new Vector2(0.5f, 0.5f);
            createdContainer.anchorMax = new Vector2(0.5f, 0.5f);
            createdContainer.pivot = new Vector2(0.5f, 0.5f);
            createdContainer.sizeDelta = new Vector2(width, height);
        }

        // The box drawn when the scene gives no prefab: a faint fill, four solid edges and the label.
        // The earlier one never showed: its copies stayed inactive, they were placed from the
        // container's centre instead of its corner, and their outline took the fill's 5% alpha.
        private GameObject BuildRuntimePrefab(Transform parent)
        {
            const float edge = 6f; // in frame pixels
            var boxColor = new Color(0.2f, 1f, 0.3f, 0.95f);

            var root = new GameObject("BBoxPrefab", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.SetActive(false);

            // RenderBoxes places a box by its bottom-left corner, from the container's bottom-left.
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;

            var fill = root.AddComponent<Image>();
            fill.color = new Color(boxColor.r, boxColor.g, boxColor.b, 0.08f);
            fill.raycastTarget = false;

            AddPart(rect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, edge), boxColor);
            AddPart(rect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, edge), boxColor);
            AddPart(rect, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(edge, 0f), boxColor);
            AddPart(rect, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(edge, 0f), boxColor);

            // The detection's label, just above the box's top-left corner.
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, edge);
            labelRect.sizeDelta = new Vector2(480f, 44f);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 36f;
            label.color = boxColor;
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = false; // labels come from the vision server
            label.raycastTarget = false;

            return root;
        }

        private static void AddPart(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                    Vector2 pivot, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        public void Clear()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i] != null)
                {
                    Destroy(_instances[i]);
                }
            }
            _instances.Clear();
        }
    }
}

