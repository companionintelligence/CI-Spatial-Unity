using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CompanionScan.Core;
using NaughtyAttributes;
using Newtonsoft.Json.Linq;
using PassthroughCameraSamples;
using PresentFutures.XRAI;
using UnityEngine;
using UnityEngine.UI;
using Meta.XR;

namespace PresentFutures.XRAI.Florence
{
    public enum Florence2Task
    {
        Caption,
        DetailedCaption,
        MoreDetailedCaption,
        ObjectDetection,
        DenseRegionCaption,
        RegionProposal,
        CaptionToPhraseGrounding,
        ReferringExpressionSegmentation,
        RegionToSegmentation,
        OpenVocabularyDetection,
        RegionToCategory,
        RegionToDescription,
        OCR,
        OCRWithRegion
    }

    public class Florence2Controller : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private ServerConfig serverConfig;

        [Header("Input")]
        [Tooltip("The image you want to process")]
        public RawImage sourceTexture;

        [Header("Task Selection")]
        public Florence2Task task;
        [Tooltip("E.g., for CaptionToPhraseGrounding or OpenVocabularyDetection")]
        public string textPrompt;
        [Tooltip("E.g., for RegionTo... tasks. Uses normalized coordinates (0-1).")]
        public Rect regionOfInterest = new Rect(0.25f, 0.25f, 0.5f, 0.5f);

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

        public enum FlorenceAnchorMode { BoundingBox2D, SpatialLabel3D, Both }

        [Header("Anchor Mode")]
        [Tooltip("Choose how to visualize detections: 2D bounding boxes or 3D spatial anchors")]
        public FlorenceAnchorMode anchorMode = FlorenceAnchorMode.BoundingBox2D;

        [Header("Spatial Placement")]
        [Tooltip("Prefab to instantiate at each detected object position")]
        public GameObject spatialAnchorPrefab;
        [Tooltip("Reference to the EnvironmentRaycastManager in the scene")]
        public EnvironmentRaycastManager environmentRaycastManager;

        private readonly List<DetectionBox> _detectionResults = new List<DetectionBox>();
        private readonly List<GameObject> _spawnedBoxes = new List<GameObject>();
        private readonly List<GameObject> _spawnedAnchors = new List<GameObject>();
        private CVServerClient _client;

        private readonly Dictionary<Florence2Task, string> _taskPrompts = new Dictionary<Florence2Task, string>
        {
            { Florence2Task.Caption, "<CAPTION>" },
            { Florence2Task.DetailedCaption, "<DETAILED_CAPTION>" },
            { Florence2Task.MoreDetailedCaption, "<MORE_DETAILED_CAPTION>" },
            { Florence2Task.ObjectDetection, "<OD>" },
            { Florence2Task.DenseRegionCaption, "<DENSE_REGION_CAPTION>" },
            { Florence2Task.RegionProposal, "<REGION_PROPOSAL>" },
            { Florence2Task.CaptionToPhraseGrounding, "<CAPTION_TO_PHRASE_GROUNDING>" },
            { Florence2Task.ReferringExpressionSegmentation, "<REFERRING_EXPRESSION_SEGMENTATION>" },
            { Florence2Task.RegionToSegmentation, "<REGION_TO_SEGMENTATION>" },
            { Florence2Task.OpenVocabularyDetection, "<OPEN_VOCABULARY_DETECTION>" },
            { Florence2Task.RegionToCategory, "<REGION_TO_CATEGORY>" },
            { Florence2Task.RegionToDescription, "<REGION_TO_DESCRIPTION>" },
            { Florence2Task.OCR, "<OCR>" },
            { Florence2Task.OCRWithRegion, "<OCR_WITH_REGION>" }
        };

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
                Debug.LogError("Florence2Controller is missing a source texture.");
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

            string taskPrompt = _taskPrompts[task];
            string textInput = string.Empty;
            if (task == Florence2Task.CaptionToPhraseGrounding || task == Florence2Task.ReferringExpressionSegmentation || task == Florence2Task.OpenVocabularyDetection)
            {
                textInput = textPrompt;
            }

            Task<FlorenceResult> requestTask = _client.FlorenceAsync(imageBytes, taskPrompt, textInput);
            while (!requestTask.IsCompleted)
            {
                yield return null;
            }

            if (requestTask.IsFaulted)
            {
                Debug.LogError($"Florence2Controller request failed: {requestTask.Exception}");
                if (statusText != null) statusText.text = "Error: Request failed.";
                if (loadingIcon != null) loadingIcon.SetActive(false);
                yield break;
            }

            var result = requestTask.Result;
            if (result == null)
            {
                if (statusText != null) statusText.text = "Error: Empty response.";
                if (loadingIcon != null) loadingIcon.SetActive(false);
                yield break;
            }

            if (resultText != null)
            {
                // Prefer the plain-text field for captions; fall back to full results JSON
                resultText.text = !string.IsNullOrEmpty(result.text)
                    ? result.text
                    : (result.results != null ? result.results.ToString() : "No results.");
            }

            if (TryExtractDetections(result.results, out var detections))
            {
                _detectionResults.Clear();
                _detectionResults.AddRange(detections);
                StartCoroutine(SpawnDetectionVisuals(_detectionResults));
            }

            if (statusText != null) statusText.text = "Done!";
            if (loadingIcon != null) loadingIcon.SetActive(false);
        }

        private bool TryExtractDetections(JToken results, out List<DetectionBox> detections)
        {
            detections = new List<DetectionBox>();
            if (results == null)
            {
                return false;
            }

            JToken entityToken = results["entities"] ?? results;
            var labelsToken = entityToken["labels"] as JArray;
            var bboxesToken = entityToken["bboxes"] as JArray;

            if (labelsToken == null || bboxesToken == null)
            {
                return false;
            }

            GetImageSize(out int width, out int height);
            int count = Mathf.Min(labelsToken.Count, bboxesToken.Count);

            for (int i = 0; i < count; i++)
            {
                if (bboxesToken[i] is not JArray bbox || bbox.Count < 4)
                {
                    continue;
                }

                float x1 = bbox[0].Value<float>();
                float y1 = bbox[1].Value<float>();
                float x2 = bbox[2].Value<float>();
                float y2 = bbox[3].Value<float>();

                if (!BoundingBoxUtils.TryNormalize(new[] { x1, y1, x2, y2 }, width, height, out Rect rect))
                {
                    continue;
                }

                detections.Add(new DetectionBox
                {
                    BoundingBox = rect,
                    Label = labelsToken[i]?.ToString() ?? "object"
                });
            }

            return detections.Count > 0;
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

        private IEnumerator SpawnDetectionVisuals(List<DetectionBox> results)
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

                if (anchorMode == FlorenceAnchorMode.BoundingBox2D || anchorMode == FlorenceAnchorMode.Both)
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
                if ((anchorMode == FlorenceAnchorMode.SpatialLabel3D || anchorMode == FlorenceAnchorMode.Both) && spatialAnchorPrefab != null && environmentRaycastManager != null)
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
    }
}

