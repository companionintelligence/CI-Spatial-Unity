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

        private readonly List<GameObject> _instances = new List<GameObject>();

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
                var rect = boxObj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));
                rect.anchoredPosition = new Vector2(x1, containerRect.height - y2);

                var label = boxObj.GetComponentInChildren<TMP_Text>();
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

        private GameObject BuildRuntimePrefab(Transform parent)
        {
            var root = new GameObject("BBoxPrefab", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.SetActive(false);

            var image = root.AddComponent<Image>();
            image.color = new Color(0f, 1f, 0f, 0.05f);
            var outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 1f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            return root;
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

