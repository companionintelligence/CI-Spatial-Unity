using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace HouseMapper.Core
{
    public class CVServerClient
    {
        private readonly string baseUrl;
        private readonly int timeoutSeconds;

        public CVServerClient(ServerConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            baseUrl = config.BaseUrl.TrimEnd('/');
            timeoutSeconds = config.RequestTimeoutSeconds;
        }

        public Task<DetectionResult> DetectAsync(byte[] jpegBytes, string prompt, float? boxThreshold = null, float? textThreshold = null)
        {
            var payload = new DetectionRequest
            {
                Image = Convert.ToBase64String(jpegBytes),
                Prompt = prompt,
                BoxThreshold = boxThreshold ?? 0.35f,
                TextThreshold = textThreshold ?? 0.25f
            };
            return PostAsync<DetectionResult>("/v1/detect", payload);
        }

        public Task<SegmentResult> SegmentAsync(byte[] jpegBytes, string prompt, string groundingModel = "grounding_dino", string maskFormat = "rle", bool returnMasks = true, float? boxThreshold = null, float? textThreshold = null)
        {
            var payload = new SegmentRequest
            {
                Image = Convert.ToBase64String(jpegBytes),
                Prompt = prompt,
                BoxThreshold = boxThreshold ?? 0.35f,
                TextThreshold = textThreshold ?? 0.25f,
                GroundingModel = groundingModel,
                MaskFormat = maskFormat,
                ReturnMasks = returnMasks
            };
            return PostAsync<SegmentResult>("/v1/segment", payload);
        }

        public Task<FlorenceResult> FlorenceAsync(byte[] jpegBytes, string task, string textInput = "")
        {
            var payload = new FlorenceRequest
            {
                Image = Convert.ToBase64String(jpegBytes),
                Task = task,
                TextInput = textInput
            };
            return PostAsync<FlorenceResult>("/v1/florence", payload);
        }

        private async Task<T> PostAsync<T>(string endpoint, object payload)
        {
            var url = baseUrl + endpoint;
            var jsonPayload = JsonConvert.SerializeObject(payload);
            var bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                if (timeoutSeconds > 0)
                {
                    request.timeout = timeoutSeconds;
                }

                await SendRequestAsync(request).ConfigureAwait(false);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var errorText = string.IsNullOrEmpty(request.error) ? "Unknown error" : request.error;
                    var responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    throw new InvalidOperationException($"Request failed: {errorText}. Response: {responseText}");
                }

                var responseJson = request.downloadHandler.text;
                return JsonConvert.DeserializeObject<T>(responseJson);
            }
        }

        private static Task<UnityWebRequest> SendRequestAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            var operation = request.SendWebRequest();
            operation.completed += _ => tcs.TrySetResult(request);
            return tcs.Task;
        }
    }
}
