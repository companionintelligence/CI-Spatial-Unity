using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CompanionScan.Core;
using NaughtyAttributes;
using PassthroughCameraSamples;
using PresentFutures.XRAI;
using UnityEngine;
using UnityEngine.UI;
using Meta.XR;

namespace PresentFutures.XRAI.GroundingDino
{
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

        private readonly List<DetectionBox> _detectionResults = new List<DetectionBox>();
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

            Task<CompanionScan.Core.DetectionResult> detectTask = _client.DetectAsync(imageBytes, textPrompt);
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

        private void PopulateDetections(CompanionScan.Core.DetectionResult detection)
        {
            _detectionResults.Clear();

            if (detection.bboxes == null || detection.labels == null)
            {
                return;
            }

            int count = Mathf.Min(detection.bboxes.Count, detection.labels.Count);
            GetImageSize(out int width, out int height);

            for (int i = 0; i < count; i++)
            {
                float score = i < detection.scores.Count ? detection.scores[i] : 0f;
                if (score < threshold)
                {
                    continue;
                }

                if (!BoundingBoxUtils.TryNormalize(detection.bboxes[i], width, height, out Rect rect))
                {
                    continue;
                }

                _detectionResults.Add(new DetectionBox
                {
                    BoundingBox = rect,
                    Label = $"{detection.labels[i]} ({score:F2})"
                });
            }
        }

        private void GetImageSize(out int width, out int height)
        {
            width = 0;
            height = 0;

            if (sourceTexture != null && sourceTexture.texture != null)
            {
                width = sourceTexture.texture.width;
                height = sourceTexture.texture.height;
                return;
            }

            if (resultImage != null && resultImage.texture != null)
            {
                width = resultImage.texture.width;
                height = resultImage.texture.height;
            }
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

        public IEnumerator SpawnDetectionVisuals(List<DetectionBox> results)
        {
            if (boundingBoxContainer == null)
            {
                Debug.LogWarning("No boundingBoxContainer assigned â€“ falling back to OnGUI drawing.");
                yield break;
            }

            if (resultImage == null || resultImage.texture == null)
            {
                Debug.LogWarning("No resultImage texture assigned â€“ cannot render bounding boxes.");
                yield break;
            }

            ClearBoundingBoxes();

            RectTransform imgRect = resultImage.rectTransform;
            int imageWidth = resultImage.texture.width;
            int imageHeight = resultImage.texture.height;
            float scaleX = imgRect.rect.width / imageWidth;
            float scaleY = imgRect.rect.height / imageHeight;

            foreach (var det in results)
            {
                if (boundingBoxPrefab == null)
                {
                    Debug.LogError("boundingBoxPrefab not assigned");
                    yield break;
                }
                float x = det.BoundingBox.x * imageWidth;
                float y = det.BoundingBox.y * imageHeight;
                float w = det.BoundingBox.width * imageWidth;
                float h = det.BoundingBox.height * imageHeight;

                float uiX = x * scaleX;
                float uiY = y * scaleY;
                float uiW = w * scaleX;
                float uiH = h * scaleY;

                if (anchorMode == AnchorMode.BoundingBox2D || anchorMode == AnchorMode.Both)
                {
                    GameObject boxGO = Instantiate(boundingBoxPrefab, boundingBoxContainer);
                    boxGO.name = "BBox_" + det.Label;
                    var rt = boxGO.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 1);
                    rt.anchoredPosition = new Vector2(uiX, -uiY);
                    rt.sizeDelta = new Vector2(uiW, uiH);

                    var txt = boxGO.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (txt) txt.text = det.Label;

                    _spawnedBoxes.Add(boxGO);
                }
                if ((anchorMode == AnchorMode.SpatialLabel3D || anchorMode == AnchorMode.Both) && spatialAnchorPrefab != null && environmentRaycastManager != null)
                {
                    int centerX = Mathf.RoundToInt(x + w * 0.5f);
                    int centerY = Mathf.RoundToInt(y + h * 0.5f);
                    int invertedCenterY = imageHeight - centerY;
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

