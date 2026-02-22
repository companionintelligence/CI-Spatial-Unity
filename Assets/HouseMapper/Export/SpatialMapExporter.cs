using System;
using System.Collections.Generic;
using System.IO;
using HouseMapper.Mapping;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityGLTF;

namespace HouseMapper.Export
{
    public class SpatialMapExporter : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private Transform exportRoot;
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private BoundaryProvider boundaryProvider;

        [Header("Floorplan")]
        [SerializeField] private bool includeFloorMesh = true;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private float floorY = 0.0f;

        [Header("Output")]
        [SerializeField] private string fileName = "spatial-map";
        [SerializeField] private bool writeExtras = true;

        public void Export()
        {
            Transform root = exportRoot != null ? exportRoot : transform;
            var outputDir = Path.Combine(Application.persistentDataPath, "exports");
            Directory.CreateDirectory(outputDir);

            var floorObject = CreateFloorMesh(root);

            var exporter = new GLTFSceneExporter(new[] { root });
            exporter.SaveGLTFandBin(outputDir, fileName);

            var gltfPath = Path.Combine(outputDir, fileName + ".gltf");
            if (writeExtras)
            {
                var extras = BuildExtras(root);
                GltfExtrasWriter.WriteExtras(gltfPath, extras);
            }

            if (floorObject != null)
            {
                Destroy(floorObject);
            }

            Debug.Log($"glTF export complete: {gltfPath}");
        }

        private GameObject CreateFloorMesh(Transform root)
        {
            if (!includeFloorMesh)
            {
                return null;
            }

            var boundary = boundaryProvider != null ? boundaryProvider.GetBoundaryWorldPoints() : new List<Vector3>();
            var pins = pinManager != null ? pinManager.Pins : null;
            var polygon = FloorplanBuilder.BuildFloorPolygon(boundary, pins, floorY);
            var mesh = FloorplanBuilder.BuildFloorMesh(polygon, root, floorY);
            if (mesh == null)
            {
                return null;
            }

            var floorObj = new GameObject("FloorplanMesh");
            floorObj.transform.SetParent(root, false);
            var filter = floorObj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = floorObj.AddComponent<MeshRenderer>();
            if (floorMaterial != null)
            {
                renderer.sharedMaterial = floorMaterial;
            }

            return floorObj;
        }

        private JObject BuildExtras(Transform root)
        {
            var extras = new JObject
            {
                ["schema_version"] = "1.0",
                ["timestamp_utc"] = DateTime.UtcNow.ToString("o"),
                ["units"] = "meters"
            };

            var boundary = boundaryProvider != null ? boundaryProvider.GetBoundaryWorldPoints() : new List<Vector3>();
            var pins = pinManager != null ? pinManager.Pins : new List<SpatialPin>();
            var bounds = FloorplanBuilder.ComputeBounds(boundary, pins);

            extras["bounds"] = new JObject
            {
                ["center"] = ToArray(bounds.center),
                ["size"] = ToArray(bounds.size)
            };

            extras["boundary"] = ToArray(boundary);

            var floorPolygon = FloorplanBuilder.BuildFloorPolygon(boundary, pins, floorY);
            extras["floorplan"] = ToArray(floorPolygon);

            var pinArray = new JArray();
            for (int i = 0; i < pins.Count; i++)
            {
                var pin = pins[i];
                var pinObj = new JObject
                {
                    ["label"] = pin.label,
                    ["score"] = pin.score,
                    ["position"] = ToArray(pin.position),
                    ["normal"] = ToArray(pin.normal),
                    ["bbox_pixels"] = new JArray(pin.bboxPixels.x, pin.bboxPixels.y, pin.bboxPixels.width, pin.bboxPixels.height),
                    ["bbox_normalized"] = new JArray(pin.bboxNormalized.x, pin.bboxNormalized.y, pin.bboxNormalized.width, pin.bboxNormalized.height),
                    ["prompt"] = pin.sourcePrompt,
                    ["timestamp"] = pin.timestamp
                };
                pinArray.Add(pinObj);
            }
            extras["pins"] = pinArray;

            return extras;
        }

        private static JArray ToArray(Vector3 value)
        {
            return new JArray(value.x, value.y, value.z);
        }

        private static JArray ToArray(IReadOnlyList<Vector3> values)
        {
            var array = new JArray();
            if (values == null)
            {
                return array;
            }

            for (int i = 0; i < values.Count; i++)
            {
                array.Add(ToArray(values[i]));
            }
            return array;
        }
    }
}
