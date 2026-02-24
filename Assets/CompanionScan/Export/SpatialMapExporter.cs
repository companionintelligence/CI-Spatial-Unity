using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CompanionScan.Mapping;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CompanionScan.Export
{
    public class SpatialMapExporter : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private Transform exportRoot;
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private BoundaryProvider boundaryProvider;
        [SerializeField] private MetaSceneCollector metaSceneCollector;
        [SerializeField] private SnapshotFrameStore snapshotStore;

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

            var baseOutputDir = Path.Combine(Application.persistentDataPath, "exports");
            Directory.CreateDirectory(baseOutputDir);

            MetaSceneSnapshot metaSnapshot = null;
            if (captureMeta && metaSceneCollector != null)
            {
                metaSnapshot = await metaSceneCollector.CaptureSnapshotAsync();
            }

            var spaceId = ResolveSpaceId(metaSnapshot, metaSceneCollector?.LastSnapshot);
            var spaceToken = SanitizeFileToken(spaceId);
            var outputDir = Path.Combine(baseOutputDir, spaceToken);
            Directory.CreateDirectory(outputDir);

            var exportName = BuildExportName(spaceToken);

            var floorObject = CreateFloorMesh(root);

            var snapshotExtras = BuildSnapshotExtras(outputDir);
            var existingExtras = ReadExistingExtras(Path.Combine(outputDir, exportName + ".gltf"));

            if (!TryExportGltf(root, outputDir, exportName))
            {
                Debug.LogError("UnityGLTF is not available. Install the UnityGLTF package to export.");
                if (floorObject != null)
                {
                    Destroy(floorObject);
                }
                return new ExportResult
                {
                    gltfPath = string.Empty,
                    binPath = string.Empty,
                    extras = returnExtras ? BuildExtras(root, metaSnapshot, snapshotExtras, spaceId) : null
                };
            }

            var gltfPath = Path.Combine(outputDir, exportName + ".gltf");
            var binPath = Path.Combine(outputDir, exportName + ".bin");
            var extras = BuildExtras(root, metaSnapshot, snapshotExtras, spaceId);
            extras = MergeExtras(existingExtras, extras);
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

        private JObject BuildExtras(Transform root, MetaSceneSnapshot metaSnapshot, JArray snapshotExtras, string spaceId)
        {
            var extras = new JObject
            {
                ["schema_version"] = "1.0",
                ["timestamp_utc"] = DateTime.UtcNow.ToString("o"),
                ["units"] = "meters"
            };

            var boundary = boundaryProvider != null ? boundaryProvider.GetBoundaryWorldPoints() : new List<Vector3>();
            var pins = pinManager != null ? pinManager.Pins : new List<SpatialPin>();
            var floorPolygon = FloorplanBuilder.BuildFloorPolygon(boundary, pins, floorY);
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
                    ["semantic_label"] = pin.semanticLabel,
                    ["score"] = pin.score,
                    ["source"] = pin.source.ToString(),
                    ["position"] = ToArray(pin.position),
                    ["normal"] = ToArray(pin.normal),
                    ["rotation"] = new JArray(pin.rotation.x, pin.rotation.y, pin.rotation.z, pin.rotation.w),
                    ["bbox_pixels"] = new JArray(pin.bboxPixels.x, pin.bboxPixels.y, pin.bboxPixels.width, pin.bboxPixels.height),
                    ["bbox_normalized"] = new JArray(pin.bboxNormalized.x, pin.bboxNormalized.y, pin.bboxNormalized.width, pin.bboxNormalized.height),
                    ["plane_size"] = ToArray(pin.planeSize),
                    ["volume_size"] = ToArray(pin.volumeSize),
                    ["anchor_uuid"] = pin.anchorUuid ?? "",
                    ["prompt"] = pin.sourcePrompt,
                    ["timestamp"] = pin.timestamp
                };
                pinArray.Add(pinObj);
            }
            cvMapping["pins"] = pinArray;
            if (!string.IsNullOrWhiteSpace(spaceId))
            {
                cvMapping["space_id"] = spaceId;
            }

            if (snapshotExtras != null)
            {
                cvMapping["snapshots"] = snapshotExtras;
            }

            extras["cv_mapping"] = cvMapping;
            extras["meta_scene"] = BuildMetaSceneExtras(metaSnapshot);
            if (!string.IsNullOrWhiteSpace(spaceId))
            {
                extras["space_id"] = spaceId;
            }

            extras["bounds"] = cvMapping["bounds"];
            extras["boundary"] = cvMapping["boundary"];
            extras["floorplan"] = cvMapping["floorplan"];
            extras["pins"] = pinArray;
            if (snapshotExtras != null)
            {
                extras["snapshots"] = snapshotExtras;
            }

            return extras;
        }

        private JArray BuildSnapshotExtras(string outputDir)
        {
            if (snapshotStore == null)
            {
                return null;
            }

            var frames = snapshotStore.ConsumeFrames();
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            var snapshotDir = Path.Combine(outputDir, "snapshots");
            Directory.CreateDirectory(snapshotDir);

            var array = new JArray();
            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                if (frame == null || frame.jpegBytes == null || frame.jpegBytes.Length == 0)
                {
                    continue;
                }

                string fileId = string.IsNullOrWhiteSpace(frame.id) ? $"snap_{i:0000}" : frame.id;
                string fileName = fileId + ".jpg";
                string filePath = Path.Combine(snapshotDir, fileName);
                File.WriteAllBytes(filePath, frame.jpegBytes);

                var entry = new JObject
                {
                    ["id"] = fileId,
                    ["timestamp"] = frame.timestamp,
                    ["image"] = Path.Combine("snapshots", fileName).Replace("\\", "/"),
                    ["image_size"] = new JArray(frame.imageWidth, frame.imageHeight),
                    ["camera"] = new JObject
                    {
                        ["position"] = ToArray(frame.cameraPosition),
                        ["rotation"] = new JArray(frame.cameraRotation.x, frame.cameraRotation.y, frame.cameraRotation.z, frame.cameraRotation.w)
                    },
                    ["frustum"] = new JObject
                    {
                        ["distance"] = frame.frustumDistance,
                        ["corners"] = ToArray(frame.frustumCorners)
                    },
                    ["intrinsics"] = new JArray(
                        frame.cameraIntrinsics.m00, frame.cameraIntrinsics.m01, frame.cameraIntrinsics.m02, frame.cameraIntrinsics.m03,
                        frame.cameraIntrinsics.m10, frame.cameraIntrinsics.m11, frame.cameraIntrinsics.m12, frame.cameraIntrinsics.m13,
                        frame.cameraIntrinsics.m20, frame.cameraIntrinsics.m21, frame.cameraIntrinsics.m22, frame.cameraIntrinsics.m23,
                        frame.cameraIntrinsics.m30, frame.cameraIntrinsics.m31, frame.cameraIntrinsics.m32, frame.cameraIntrinsics.m33)
                };

                array.Add(entry);
            }

            return array;
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
                ["room_uuids"] = new JArray(metaSnapshot.roomUuids),
                ["primary_room_uuid"] = metaSnapshot.primaryRoomUuid ?? string.Empty,
                ["anchors"] = anchors
            };

            return metaScene;
        }

        private static string ResolveSpaceId(MetaSceneSnapshot current, MetaSceneSnapshot fallback)
        {
            var snapshot = current ?? fallback;
            if (snapshot == null)
            {
                return "default";
            }

            if (!string.IsNullOrWhiteSpace(snapshot.primaryRoomUuid))
            {
                return snapshot.primaryRoomUuid;
            }

            if (snapshot.roomUuids != null && snapshot.roomUuids.Count > 0)
            {
                return snapshot.roomUuids[0];
            }

            if (snapshot.anchors != null && snapshot.anchors.Count > 0)
            {
                return snapshot.anchors[0].uuid;
            }

            return "default";
        }

        private string BuildExportName(string spaceToken)
        {
            var baseName = string.IsNullOrWhiteSpace(fileName) ? "spatial-map" : fileName;
            return string.IsNullOrWhiteSpace(spaceToken) ? baseName : $"{baseName}_{spaceToken}";
        }

        private static string SanitizeFileToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return "default";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = token.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = '-';
                }
            }

            return new string(chars);
        }

        private static JObject ReadExistingExtras(string gltfPath)
        {
            if (string.IsNullOrEmpty(gltfPath) || !File.Exists(gltfPath))
            {
                return null;
            }

            var root = JObject.Parse(File.ReadAllText(gltfPath));
            return root["extras"] as JObject;
        }

        private static JObject MergeExtras(JObject existing, JObject current)
        {
            if (existing == null)
            {
                return current;
            }

            if (current == null)
            {
                return existing;
            }

            var merged = new JObject(existing);

            var existingSnapshots = existing["snapshots"] as JArray;
            var currentSnapshots = current["snapshots"] as JArray;
            merged["snapshots"] = MergeArrays(existingSnapshots, currentSnapshots);

            var existingPins = existing["pins"] as JArray;
            var currentPins = current["pins"] as JArray;
            merged["pins"] = MergeArrays(existingPins, currentPins);

            var existingCv = existing["cv_mapping"] as JObject;
            var currentCv = current["cv_mapping"] as JObject;
            var mergedCv = currentCv != null ? new JObject(currentCv) : new JObject();
            if (existingCv != null)
            {
                var cvSnapshots = MergeArrays(existingCv["snapshots"] as JArray, currentCv?["snapshots"] as JArray);
                var cvPins = MergeArrays(existingCv["pins"] as JArray, currentCv?["pins"] as JArray);
                if (cvSnapshots != null) mergedCv["snapshots"] = cvSnapshots;
                if (cvPins != null) mergedCv["pins"] = cvPins;
            }
            merged["cv_mapping"] = mergedCv;

            if (current["meta_scene"] != null)
            {
                merged["meta_scene"] = current["meta_scene"];
            }

            if (current["space_id"] != null)
            {
                merged["space_id"] = current["space_id"];
            }

            return merged;
        }

        private static JArray MergeArrays(JArray existing, JArray current)
        {
            if ((existing == null || existing.Count == 0) && (current == null || current.Count == 0))
            {
                return null;
            }

            var merged = new JArray();
            if (existing != null)
            {
                foreach (var item in existing)
                {
                    merged.Add(item);
                }
            }
            if (current != null)
            {
                foreach (var item in current)
                {
                    merged.Add(item);
                }
            }
            return merged;
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

        private bool TryExportGltf(Transform root, string outputDir, string exportName)
        {
            var exporterType = Type.GetType("UnityGLTF.GLTFSceneExporter, UnityGLTF");
            if (exporterType == null)
            {
                return false;
            }

            var ctor = exporterType.GetConstructor(new[] { typeof(Transform[]) });
            if (ctor == null)
            {
                return false;
            }

            var exporter = ctor.Invoke(new object[] { new[] { root } });
            var saveMethod = exporterType.GetMethod("SaveGLTFandBin", new[] { typeof(string), typeof(string) });
            if (saveMethod == null)
            {
                return false;
            }

            saveMethod.Invoke(exporter, new object[] { outputDir, exportName });
            return true;
        }
    }
}

