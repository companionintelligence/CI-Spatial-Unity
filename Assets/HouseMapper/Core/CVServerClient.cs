using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace HouseMapper.Core
{
    public class CVServerClient
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
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(request.error);
            }

            return JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
        }
    }
}
