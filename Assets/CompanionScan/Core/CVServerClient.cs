using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace CompanionScan.Core
{
    public class CVServerClient : ICvInferenceClient
    {
        private readonly ServerConfig _config;

        public CVServerClient(ServerConfig config)
        {
            _config = config;
        }

        public Task<DetectionResult> DetectAsync(byte[] jpegBytes, string prompt)
        {
            var payload = new
            {
                image = Convert.ToBase64String(jpegBytes),
                prompt = prompt,
                box_threshold = _config.boxThreshold,
                text_threshold = _config.textThreshold
            };
            return PostAsync<DetectionResult>("/v1/detect", payload);
        }

        public Task<SegmentationResult> SegmentAsync(byte[] jpegBytes, string prompt, string groundingModel = "grounding_dino")
        {
            var payload = new
            {
                image = Convert.ToBase64String(jpegBytes),
                prompt = prompt,
                grounding_model = groundingModel,
                return_masks = true,
                mask_format = "rle",
                box_threshold = _config.boxThreshold,
                text_threshold = _config.textThreshold
            };
            return PostAsync<SegmentationResult>("/v1/segment", payload);
        }

        public Task<FlorenceResult> FlorenceAsync(byte[] jpegBytes, string task, string textInput = "")
        {
            var payload = new
            {
                image = Convert.ToBase64String(jpegBytes),
                task = task,
                text_input = textInput
            };
            return PostAsync<FlorenceResult>("/v1/florence", payload);
        }

        private async Task<T> PostAsync<T>(string endpoint, object payload)
        {
            var baseUrl = _config != null ? _config.ResolveBaseUrl() : null;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new Exception("CV server base URL is not set.");
            }

            var url = baseUrl.TrimEnd('/') + endpoint;
            var json = JsonConvert.SerializeObject(payload);
            int retries = Mathf.Max(0, _config != null ? _config.retryCount : 0);
            int timeout = Mathf.Max(1, _config != null ? _config.requestTimeoutSeconds : 6);
            float delaySeconds = _config != null ? _config.retryDelaySeconds : 0.5f;

            Exception lastError = null;
            for (int attempt = 0; attempt <= retries; attempt++)
            {
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = timeout;

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
                }

                string error = request.error;
                long status = request.responseCode;
                lastError = new Exception($"CV server error ({status}): {error}");

                if (attempt < retries)
                {
                    int delayMs = Mathf.Max(0, Mathf.RoundToInt(delaySeconds * 1000f * (attempt + 1)));
                    await Task.Delay(delayMs, CancellationToken.None);
                }
            }

            throw lastError ?? new Exception("CV server request failed.");
        }
    }
}

