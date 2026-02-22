using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HouseMapper.Core;
using NaughtyAttributes;
using PassthroughCameraSamples;
using UnityEngine;
using UnityEngine.UI;
using Meta.XR;

namespace PresentFutures.XRAI.GroundingDino
{
    public class DetectionResult
    {
        public Rect BoundingBox;
        public string Label;
    }

    public class GroundingDinoController : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private ServerConfig serverConfig;

        [Header("Input")]
        [Tooltip("The image you want to process")]
        public RawImage sourceTexture;

        [Header("Task Selection")]
        [Tooltip("A natural language prompt describing what to detect. E.g., 'a cat, a dog, and a car'")]
        public string textPrompt;

        [Tooltip("Confidence threshold for detections (0.0 to 1.0)")]
        [Range(0f, 1f)]
        public float threshold = 0.3f;

        [Header("UI Elements")]
        public TMPro.TMP_Text resultText;
        public RawImage resultImage;
        [Tooltip("UI container (RectTransform) that overlays the result image and will receive the bounding-box UI elements")]
        public RectTransform boundingBoxContainer;
        [Tooltip("Prefab with RectTransform root and TextMeshProUGUI label child")] public GameObject boundingBoxPrefab;
        public TMPro.TMP_Text statusText;

        [Header("Loading UI")]
        [Tooltip("UI GameObject (e.g. spinner) to show while waiting for results")]
        public GameObject loadingIcon;

        public enum AnchorMode { BoundingBox2D, SpatialLabel3D, Both }

        [Header("Anchor Mode")]
        [Tooltip("Choose how to visualize detections: 2D bounding boxes or 3D spatial anchors")]
        public AnchorMode anchorMode = AnchorMode.BoundingBox2D;

        [Header("Spatial Placement")]
        [Tooltip("Prefab to instantiate at each detected object position")]
        public GameObject spatialAnchorPrefab;
        [Tooltip("Reference to the EnvironmentRaycastManager in the scene")]
        public EnvironmentRaycastManager environmentRaycastManager;

        private readonly List<DetectionResult> _detectionResults = new List<DetectionResult>();
        private readonly List<GameObject> _spawnedBoxes = new List<GameObject>();
        private readonly List<GameObject> _spawnedAnchors = new List<GameObject>();
        private CVServerClient _client;

        private void Awake()
        {
            if (serverConfig == null)
            {
                serverConfig = ScriptableObject.CreateInstance<ServerConfig>();
            }
            _client = new CVServerClient(serverConfig);
        }

        [Button]
        public void SendRequest()
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            if (!enabled)
            {
                enabled = true;
            }

            if (sourceTexture == null)
            {
                Debug.LogError("GroundingDinoController is missing a source texture.");
                if (statusText != null) statusText.text = "Error: Missing source image.";
                if (loadingIcon != null) loadingIcon.SetActive(false);
                return;
            }

            if (resultText != null) resultText.text = "";
            if (statusText != null) statusText.text = "Processing...";
            if (loadingIcon != null) loadingIcon.SetActive(true);

            _detectionResults.Clear();
            StartCoroutine(SendApiRequest());
        }

        private IEnumerator SendApiRequest()
        {
            byte[] imageBytes = EncodeTextureToJPG(sourceTexture.texture);
            if (imageBytes == null || imageBytes.Length == 0)
            {
                if (statusText != null) statusText.text = "Error: Failed to capture image.";
                if (loadingIcon != null) loadingIcon.SetActive(false);
                yield break;
            }

            Task<HouseMapper.Core.DetectionResult> detectTask = _client.DetectAsync(imageBytes, textPrompt);
            while (!detectTask.IsCompleted)
            {
                yield return null;
            }

            if (detectTask.IsFaulted)
            {
                Debug.LogError($"GroundingDinoController request failed: {detectTask.Exception}");
                if (statusText != null) statusText.text = "Error: Request failed.";
                if (loadingIcon != null) loadingIcon.SetActive(false);
                yield break;
            }

            var detection = detectTask.Result;
            if (detection == null)
            {
                if (statusText != null) statusText.text = "Error: Empty response.";
                if (loadingIcon != null) loadingIcon.SetActive(false);
                yield break;
            }

            if (resultText != null)
            {
                resultText.text = $"Detections: {detection.labels.Count}";
            }

            PopulateDetections(detection);
            StartCoroutine(SpawnDetectionVisuals(_detectionResults));

            if (statusText != null) statusText.text = "Done!";
            if (loadingIcon != null) loadingIcon.SetActive(false);
        }

        private void PopulateDetections(HouseMapper.Core.DetectionResult detection)
        {
            _detectionResults.Clear();

            if (detection.bboxes == null || detection.labels == null)
            {
                return;
            }

            int count = Mathf.Min(detection.bboxes.Count, detection.labels.Count);
            int width = resultImage != null && resultImage.texture != null ? resultImage.texture.width : 0;
            int height = resultImage != null && resultImage.texture != null ? resultImage.texture.height : 0;

            for (int i = 0; i < count; i++)
            {
                float score = i < detection.scores.Count ? detection.scores[i] : 0f;
                if (score < threshold)
                {
                    continue;
                }

                var rect = ToRect(detection.bboxes[i], width, height);
                if (rect.width <= 0f || rect.height <= 0f)
                {
                    continue;
                }

                _detectionResults.Add(new DetectionResult
                {
                    BoundingBox = rect,
                    Label = $"{detection.labels[i]} ({score:F2})"
                });
            }
        }

        private static Rect ToRect(float[] bbox, int imageWidth, int imageHeight)
        {
            if (bbox == null || bbox.Length < 4)
            {
                return Rect.zero;
            }

            float x1 = bbox[0];
            float y1 = bbox[1];
            float x2 = bbox[2];
            float y2 = bbox[3];

            bool normalized = x2 <= 1f && y2 <= 1f && imageWidth > 0 && imageHeight > 0;
            if (normalized)
            {
                x1 *= imageWidth;
                x2 *= imageWidth;
                y1 *= imageHeight;
                y2 *= imageHeight;
            }

            float width = x2 - x1;
            float height = y2 - y1;
            if (width < 0f || height < 0f)
            {
                width = x2;
                height = y2;
            }

            return new Rect(x1, y1, width, height);
        }

        public static Texture2D ConvertToTexture2D(Texture texture)
        {
            if (texture == null)
            {
                Debug.LogError("ConvertToTexture2D: texture is null!");
                return null;
            }

            RenderTexture tempRT = RenderTexture.GetTemporary(texture.width, texture.height, 0);
            Graphics.Blit(texture, tempRT);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tempRT;

            Texture2D tex2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            tex2D.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
            tex2D.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tempRT);

            return tex2D;
        }

        public static byte[] EncodeTextureToJPG(Texture texture, int quality = 75)
        {
            if (texture == null)
            {
                Debug.LogError("EncodeTextureToJPG: Provided texture is null!");
                return null;
            }

            if (texture.width == 0 || texture.height == 0)
            {
                Debug.LogError("EncodeTextureToJPG: Texture has invalid dimensions (0x0). Is webcam started?");
                return null;
            }

            Texture2D tex2D = ConvertToTexture2D(texture);
            return tex2D.EncodeToJPG(quality);
        }

        #region UI Bounding Box Helpers
        private void ClearBoundingBoxes()
        {
            foreach (var go in _spawnedBoxes)
            {
                if (go) Destroy(go);
            }
            _spawnedBoxes.Clear();
            ClearAnchors();
        }

        public IEnumerator SpawnDetectionVisuals(List<DetectionResult> results)
        {
            if (boundingBoxContainer == null)
            {
                Debug.LogWarning("No boundingBoxContainer assigned – falling back to OnGUI drawing.");
                yield break;
            }

            ClearBoundingBoxes();

            RectTransform imgRect = resultImage.rectTransform;
            float scaleX = imgRect.rect.width / resultImage.texture.width;
            float scaleY = imgRect.rect.height / resultImage.texture.height;

            foreach (var det in results)
            {
                if (boundingBoxPrefab == null)
                {
                    Debug.LogError("boundingBoxPrefab not assigned");
                    yield break;
                }
                float x = det.BoundingBox.x * scaleX;
                float y = det.BoundingBox.y * scaleY;
                float w = det.BoundingBox.width * scaleX;
                float h = det.BoundingBox.height * scaleY;

                if (anchorMode == AnchorMode.BoundingBox2D || anchorMode == AnchorMode.Both)
                {
                    GameObject boxGO = Instantiate(boundingBoxPrefab, boundingBoxContainer);
                    boxGO.name = "BBox_" + det.Label;
                    var rt = boxGO.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 1);
                    rt.anchoredPosition = new Vector2(x, -y);
                    rt.sizeDelta = new Vector2(w, h);

                    var txt = boxGO.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (txt) txt.text = det.Label;

                    _spawnedBoxes.Add(boxGO);
                }
                if ((anchorMode == AnchorMode.SpatialLabel3D || anchorMode == AnchorMode.Both) && spatialAnchorPrefab != null && environmentRaycastManager != null)
                {
                    int centerX = Mathf.RoundToInt(x + w * 0.5f);
                    int centerY = Mathf.RoundToInt(y + h * 0.5f);
                    int invertedCenterY = resultImage.texture.height - centerY;
                    var cameraScreenPoint = new Vector2Int(centerX, invertedCenterY);

                    var ray = PassthroughCameraUtils.ScreenPointToRayInWorld(PassthroughCameraEye.Left, cameraScreenPoint);

                    if (environmentRaycastManager.Raycast(ray, out EnvironmentRaycastHit hitInfo))
                    {
                        GameObject anchorGo = Instantiate(spatialAnchorPrefab);
                        anchorGo.transform.SetPositionAndRotation(
                            hitInfo.point,
                            Quaternion.LookRotation(hitInfo.normal, Vector3.up));
                        _spawnedAnchors.Add(anchorGo);
                        anchorGo.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = det.Label;
                    }
                }

                yield return new WaitForSeconds(0.1f);
            }
        }
        #endregion

        private void ClearAnchors()
        {
            foreach (var go in _spawnedAnchors)
            {
                if (go) Destroy(go);
            }
            _spawnedAnchors.Clear();
        }
    }
}
