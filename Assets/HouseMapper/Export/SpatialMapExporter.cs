using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        [SerializeField] private MetaSceneCollector metaSceneCollector;

        [Header("Floorplan")]
        [SerializeField] private bool includeFloorMesh = true;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private float floorY = 0.0f;

        [Header("Output")]
        [SerializeField] private string fileName = "spatial-map";
        [SerializeField] private bool writeExtras = true;
        [SerializeField] private bool captureMetaScene = true;
        [SerializeField] private bool uploadAfterExport = false;
        [SerializeField] private GltfSyncClient syncClient;

        private struct ExportResult
        {
            public string gltfPath;
            public string binPath;
            public JObject extras;
        }

        public async void Export()
        {
            await ExportInternalAsync(captureMetaScene, false);
        }

        public async void ExportAndSync()
        {
            var result = await ExportInternalAsync(captureMetaScene, true);
            if (uploadAfterExport && syncClient != null)
            {
                await syncClient.UploadAsync(result.gltfPath, result.binPath, result.extras);
            }
        }

        private async Task<ExportResult> ExportInternalAsync(bool captureMeta, bool returnExtras)
        {
            Transform root = exportRoot != null ? exportRoot : transform;
            var outputDir = Path.Combine(Application.persistentDataPath, "exports");
            Directory.CreateDirectory(outputDir);

            MetaSceneSnapshot metaSnapshot = null;
            if (captureMeta && metaSceneCollector != null)
            {
                metaSnapshot = await metaSceneCollector.CaptureSnapshotAsync();
            }

            var floorObject = CreateFloorMesh(root);

            var exporter = new GLTFSceneExporter(new[] { root });
            exporter.SaveGLTFandBin(outputDir, fileName);

            var gltfPath = Path.Combine(outputDir, fileName + ".gltf");
            var binPath = Path.Combine(outputDir, fileName + ".bin");
            var extras = BuildExtras(root, metaSnapshot);
            if (writeExtras)
            {
                GltfExtrasWriter.WriteExtras(gltfPath, extras);
            }

            if (floorObject != null)
            {
                Destroy(floorObject);
            }

            Debug.Log($"glTF export complete: {gltfPath}");
            return new ExportResult
            {
                gltfPath = gltfPath,
                binPath = binPath,
                extras = returnExtras ? extras : null
            };
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

        private JObject BuildExtras(Transform root, MetaSceneSnapshot metaSnapshot)
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

            var cvMapping = new JObject
            {
                ["bounds"] = new JObject
                {
                    ["center"] = ToArray(bounds.center),
                    ["size"] = ToArray(bounds.size)
                },
                ["boundary"] = ToArray(boundary),
                ["floorplan"] = ToArray(floorPolygon)
            };

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
                    ["semantic_label"] = pin.semanticLabel,
                    ["source"] = "cv_server",
                    ["prompt"] = pin.sourcePrompt,
                    ["timestamp"] = pin.timestamp
                };
                pinArray.Add(pinObj);
            }
            cvMapping["pins"] = pinArray;

            extras["cv_mapping"] = cvMapping;
            extras["meta_scene"] = BuildMetaSceneExtras(metaSnapshot);

            extras["bounds"] = cvMapping["bounds"];
            extras["boundary"] = cvMapping["boundary"];
            extras["floorplan"] = cvMapping["floorplan"];
            extras["pins"] = pinArray;

            return extras;
        }

        private JObject BuildMetaSceneExtras(MetaSceneSnapshot metaSnapshot)
        {
            if (metaSnapshot == null)
            {
                return new JObject
                {
                    ["available"] = false
                };
            }

            var anchors = new JArray();
            for (int i = 0; i < metaSnapshot.anchors.Count; i++)
            {
                var anchor = metaSnapshot.anchors[i];
                var anchorObj = new JObject
                {
                    ["uuid"] = anchor.uuid,
                    ["labels"] = new JArray(anchor.labels),
                    ["position"] = ToArray(anchor.position),
                    ["rotation"] = new JArray(anchor.rotation.x, anchor.rotation.y, anchor.rotation.z, anchor.rotation.w),
                    ["has_plane"] = anchor.hasPlane,
                    ["plane_center"] = ToArray(anchor.planeCenter),
                    ["plane_size"] = ToArray(anchor.planeSize),
                    ["plane_boundary"] = ToArray(anchor.planeBoundary),
                    ["has_volume"] = anchor.hasVolume,
                    ["volume_center"] = ToArray(anchor.volumeCenter),
                    ["volume_size"] = ToArray(anchor.volumeSize),
                    ["has_mesh"] = anchor.hasMesh,
                    ["mesh_vertex_count"] = anchor.meshVertexCount,
                    ["mesh_triangle_count"] = anchor.meshTriangleCount
                };
                anchors.Add(anchorObj);
            }

            var metaScene = new JObject
            {
                ["available"] = string.IsNullOrEmpty(metaSnapshot.error),
                ["error"] = metaSnapshot.error,
                ["timestamp"] = metaSnapshot.timestamp,
                ["supported_labels"] = new JArray(metaSnapshot.supportedLabels),
                ["anchors"] = anchors
            };

            return metaScene;
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
