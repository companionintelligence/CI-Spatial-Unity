using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CompanionScan.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CompanionScan.Export
{
    public class GltfSyncClient : MonoBehaviour
    {
        [SerializeField] private ServerConfig serverConfig;
        [SerializeField] private string endpointPath = "/v1/gltf";

        public async Task<bool> UploadAsync(string gltfPath, string binPath, JObject extras)
        {
            var baseUrl = serverConfig != null ? serverConfig.ResolveBaseUrl() : null;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Debug.LogError("GltfSyncClient: server base URL is not set.");
                return false;
            }

            if (!File.Exists(gltfPath))
            {
                Debug.LogError($"GltfSyncClient: glTF not found: {gltfPath}");
                return false;
            }

            var url = baseUrl.TrimEnd('/') + endpointPath;
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection(
                    "gltf",
                    File.ReadAllBytes(gltfPath),
                    Path.GetFileName(gltfPath),
                    "model/gltf+json")
            };

            if (!string.IsNullOrEmpty(binPath) && File.Exists(binPath))
            {
                form.Add(new MultipartFormFileSection(
                    "bin",
                    File.ReadAllBytes(binPath),
                    Path.GetFileName(binPath),
                    "application/octet-stream"));
            }

            if (extras != null)
            {
                var extrasJson = extras.ToString(Formatting.None);
                form.Add(new MultipartFormDataSection("extras", extrasJson, "application/json"));
            }

            using var request = UnityWebRequest.Post(url, form);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"GltfSyncClient: upload failed: {request.error}");
                return false;
            }

            Debug.Log("GltfSyncClient: upload complete.");
            return true;
        }
    }
}

