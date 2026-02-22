using System.Collections.Generic;
using HouseMapper.Core;
using TMPro;
using UnityEngine;

namespace HouseMapper.Rendering
{
    public class BBoxRenderer : MonoBehaviour
    {
        [SerializeField] private RectTransform container;
        [SerializeField] private RectTransform resultImage;
        [SerializeField] private GameObject bboxPrefab;

        private readonly List<GameObject> _instances = new List<GameObject>();

        public void RenderBoxes(DetectionResult result, Texture sourceTexture)
        {
            Clear();
            if (result == null || sourceTexture == null || container == null || bboxPrefab == null)
            {
                return;
            }

            float imgWidth = sourceTexture.width;
            float imgHeight = sourceTexture.height;
            Rect containerRect = container.rect;

            for (int i = 0; i < result.bboxes.Count; i++)
            {
                var bbox = result.bboxes[i];
                if (bbox == null || bbox.Length < 4)
                {
                    continue;
                }

                float x1 = bbox[0] / imgWidth * containerRect.width;
                float y1 = bbox[1] / imgHeight * containerRect.height;
                float x2 = bbox[2] / imgWidth * containerRect.width;
                float y2 = bbox[3] / imgHeight * containerRect.height;

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
