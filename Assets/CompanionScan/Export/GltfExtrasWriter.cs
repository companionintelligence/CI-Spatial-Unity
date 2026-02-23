using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CompanionScan.Export
{
    public static class GltfExtrasWriter
    {
        public static void WriteExtras(string gltfPath, JObject extras)
        {
            if (string.IsNullOrEmpty(gltfPath) || extras == null || !File.Exists(gltfPath))
            {
                return;
            }

            var root = JObject.Parse(File.ReadAllText(gltfPath));
            root["extras"] = extras;
            File.WriteAllText(gltfPath, root.ToString(Formatting.Indented));

            Debug.Log($"Wrote glTF extras: {gltfPath}");
        }
    }
}

