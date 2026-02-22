using System.Collections.Generic;
using System.Threading.Tasks;
using HouseMapper.Core;
using HouseMapper.Rendering;
using UnityEngine;

namespace HouseMapper.Mapping
{
    public class SpatialCapturePipeline : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private ServerConfig serverConfig;

        [Header("Camera")]
        [SerializeField] private MonoBehaviour cameraProviderSource;
        [SerializeField] private int jpegQuality = 75;

        [Header("Mapping")]
        [SerializeField] private ViewportRaycaster raycaster;
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private float minScore = 0.3f;
        [SerializeField] private int maxPinsPerCapture = 20;

        [Header("Prompts")]
        [SerializeField] private string detectionPrompt = "couch, bed, screen, table, lamp, plant, wall, floor, ceiling, other";
        [SerializeField] private string segmentationPrompt = "wall. floor. ceiling.";
        [SerializeField] private string captionTask = "<MORE_DETAILED_CAPTION>";

        [Header("Optional Rendering")]
        [SerializeField] private BBoxRenderer bboxRenderer;
        [SerializeField] private MaskRenderer maskRenderer;
        [SerializeField] private CaptionPanel captionPanel;
        [SerializeField] private bool renderBoxes = true;
        [SerializeField] private bool renderMasks = false;
        [SerializeField] private bool runSegmentation = true;
        [SerializeField] private bool runCaption = true;

        private ICameraProvider _cameraProvider;
        private CVServerClient _client;
        private bool _isBusy;
        private Texture2D _frameTexture;

        private void Awake()
        {
            _cameraProvider = cameraProviderSource as ICameraProvider;
            _client = new CVServerClient(serverConfig);
        }

        private void OnEnable()
        {
            _cameraProvider?.StartCapture();
        }

        private void OnDisable()
        {
            _cameraProvider?.StopCapture();
        }

        public async void CaptureAndPin()
        {
            if (_isBusy || _cameraProvider == null || serverConfig == null)
            {
                return;
            }

            var frame = _cameraProvider.GetCurrentFrameJpeg(jpegQuality);
            if (frame == null || frame.Length == 0)
            {
                return;
            }

            _isBusy = true;
            try
            {
                EnsureFrameTexture(frame);

                var detectionTask = _client.DetectAsync(frame, detectionPrompt);
                Task<SegmentationResult> segmentationTask = null;
                Task<FlorenceResult> captionTaskRequest = null;

                if (runSegmentation)
                {
                    segmentationTask = _client.SegmentAsync(frame, segmentationPrompt);
                }

                if (runCaption)
                {
                    captionTaskRequest = _client.FlorenceAsync(frame, captionTask);
                }

                var detection = await detectionTask;
                if (renderBoxes && bboxRenderer != null)
                {
                    bboxRenderer.RenderBoxes(detection, _frameTexture);
                }

                AddPinsFromDetections(detection);

                if (segmentationTask != null)
                {
                    var segmentation = await segmentationTask;
                    if (renderMasks && maskRenderer != null)
                    {
                        maskRenderer.RenderMasksAsync(segmentation);
                    }
                }

                if (captionTaskRequest != null)
                {
                    var caption = await captionTaskRequest;
                    if (captionPanel != null && caption != null)
                    {
                        captionPanel.SetCaption(caption.results != null ? caption.results.ToString() : string.Empty);
                    }
                }
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void EnsureFrameTexture(byte[] jpegBytes)
        {
            if (_frameTexture == null)
            {
                _frameTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            }
            _frameTexture.LoadImage(jpegBytes);
        }

        private void AddPinsFromDetections(DetectionResult detection)
        {
            if (detection == null || detection.bboxes == null || raycaster == null || pinManager == null)
            {
                return;
            }

            int count = Mathf.Min(detection.bboxes.Count, detection.labels.Count);
            int added = 0;

            for (int i = 0; i < count; i++)
            {
                if (added >= maxPinsPerCapture)
                {
                    break;
                }

                float score = i < detection.scores.Count ? detection.scores[i] : 0f;
                if (score < minScore)
                {
                    continue;
                }

                var bbox = detection.bboxes[i];
                if (bbox == null || bbox.Length < 4)
                {
                    continue;
                }

                float x1 = bbox[0];
                float y1 = bbox[1];
                float x2 = bbox[2];
                float y2 = bbox[3];

                float cx = (x1 + x2) * 0.5f;
                float cy = (y1 + y2) * 0.5f;

                float u = _frameTexture.width > 0 ? cx / _frameTexture.width : 0.5f;
                float v = _frameTexture.height > 0 ? 1.0f - (cy / _frameTexture.height) : 0.5f;

                if (!raycaster.TryRaycastViewport(new Vector2(u, v), out Vector3 hitPoint, out Vector3 hitNormal))
                {
                    hitNormal = Vector3.up;
                }

                var pin = new SpatialPin
                {
                    label = detection.labels[i],
                    score = score,
                    position = hitPoint,
                    normal = hitNormal,
                    bboxPixels = new SerializableRect(x1, y1, x2 - x1, y2 - y1),
                    bboxNormalized = new SerializableRect(
                        _frameTexture.width > 0 ? x1 / _frameTexture.width : 0f,
                        _frameTexture.height > 0 ? y1 / _frameTexture.height : 0f,
                        _frameTexture.width > 0 ? (x2 - x1) / _frameTexture.width : 0f,
                        _frameTexture.height > 0 ? (y2 - y1) / _frameTexture.height : 0f
                    ),
                    sourcePrompt = detectionPrompt,
                    timestamp = Time.timeAsDouble
                };

                pinManager.AddPin(pin);
                added++;
            }
        }
    }
}
