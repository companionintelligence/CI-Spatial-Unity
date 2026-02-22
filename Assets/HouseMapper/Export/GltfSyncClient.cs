using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HouseMapper.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace HouseMapper.Export
{
    public class GltfSyncClient : MonoBehaviour
    {
        [SerializeField] private ServerConfig serverConfig;
        [SerializeField] private string endpointPath = "/v1/gltf";

        public async Task<bool> UploadAsync(string gltfPath, string binPath, JObject extras)
        {
            if (serverConfig == null || string.IsNullOrWhiteSpace(serverConfig.baseUrl))
            {
                Debug.LogError("GltfSyncClient: serverConfig.baseUrl is not set.");
                return false;
            }

            if (!File.Exists(gltfPath))
            {
                Debug.LogError($"GltfSyncClient: glTF not found: {gltfPath}");
                return false;
            }

            var url = serverConfig.baseUrl.TrimEnd('/') + endpointPath;
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
