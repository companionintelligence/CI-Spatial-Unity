using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace CompanionScan.Mapping
{
    /// <summary>
    /// Queries the Meta Quest Scene Understanding API (OVRAnchor / OVRRoomLayout)
    /// and produces a <see cref="MetaSceneSnapshot"/> plus optional
    /// <see cref="SpatialPin"/> list for the unified pin manager.
    /// </summary>
    public class MetaSceneCollector : MonoBehaviour
    {
        [SerializeField] private Transform trackingSpace;
        [SerializeField] private bool includePlanes = true;
        [SerializeField] private bool includeVolumes = true;
        [SerializeField] private bool includeMeshes = false;
        [SerializeField] private bool includePlaneBoundary = true;

        public MetaSceneSnapshot LastSnapshot { get; private set; }

        /// <summary>
        /// Convert every anchor in the last snapshot into a <see cref="SpatialPin"/>
        /// with <c>source = PinSource.MetaScene</c>.  This lets both capture systems
        /// write to a single <see cref="SpatialPinManager"/>.
        /// </summary>
        public List<SpatialPin> ConvertSnapshotToPins(MetaSceneSnapshot snapshot = null)
        {
            snapshot ??= LastSnapshot;
            var pins = new List<SpatialPin>();
            if (snapshot?.anchors == null) return pins;

            foreach (var anchor in snapshot.anchors)
            {
                // Use the first label, or fall back to OTHER
                string label = anchor.labels != null && anchor.labels.Count > 0
                    ? anchor.labels[0]
                    : "OTHER";

                var pin = new SpatialPin
                {
                    label = label,
                    semanticLabel = label, // already a Meta label
                    score = 1f,            // ground-truth confidence
                    source = PinSource.MetaScene,
                    position = anchor.position,
                    normal = anchor.hasPlane ? (anchor.rotation * Vector3.up) : Vector3.up,
                    rotation = anchor.rotation,
                    planeSize = anchor.hasPlane ? anchor.planeSize : Vector3.zero,
                    volumeSize = anchor.hasVolume ? anchor.volumeSize : Vector3.zero,
                    anchorUuid = anchor.uuid,
                    sourcePrompt = "meta_scene_understanding",
                    timestamp = snapshot.timestamp
                };

                pins.Add(pin);
            }

            return pins;
        }

        public async Task<MetaSceneSnapshot> CaptureSnapshotAsync()
        {
            var snapshot = new MetaSceneSnapshot
            {
                timestamp = Time.timeAsDouble,
                supportedLabels = new List<string>(SemanticLabelMapper.SupportedMetaLabels)
            };

#if UNITY_ANDROID
            const string permission = "com.oculus.permission.USE_SCENE";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
            {
                snapshot.error = "Spatial Data permission not granted (com.oculus.permission.USE_SCENE).";
                LastSnapshot = snapshot;
                return snapshot;
            }
#endif

            var rooms = new List<OVRAnchor>();
            await OVRAnchor.FetchAnchorsAsync(rooms, new OVRAnchor.FetchOptions
            {
                SingleComponentType = typeof(OVRRoomLayout)
            });

            if (rooms.Count == 0)
            {
                snapshot.error = "No scene model found. Run Space Setup on-device.";
                LastSnapshot = snapshot;
                return snapshot;
            }

            var anchors = new List<OVRAnchor>();
            foreach (var room in rooms)
            {
                if (room.TryGetComponent(out OVRAnchorContainer container))
                {
                    await container.FetchAnchorsAsync(anchors);
                }
            }

            foreach (var anchor in anchors)
            {
                if (!anchor.TryGetComponent(out OVRLocatable locatable))
                {
                    continue;
                }

                await locatable.SetEnabledAsync(true);
                if (!locatable.TryGetSceneAnchorPose(out var pose))
                {
                    continue;
                }

                var worldPosition = pose.ComputeWorldPosition(trackingSpace).GetValueOrDefault();
                var worldRotation = pose.ComputeWorldRotation(trackingSpace).GetValueOrDefault();

                var labelSet = new HashSet<OVRSemanticLabels.Classification>
                {
                    OVRSemanticLabels.Classification.Other
                };
                if (anchor.TryGetComponent(out OVRSemanticLabels labels))
                {
                    labels.GetClassifications(labelSet);
                }

                var labelStrings = labelSet
                    .Select(ToMetaLabelString)
                    .Distinct()
                    .ToList();

                var anchorSnapshot = new MetaSceneAnchorSnapshot
                {
                    uuid = anchor.Uuid.ToString(),
                    labels = labelStrings,
                    position = worldPosition,
                    rotation = worldRotation
                };

                if (includePlanes && anchor.TryGetComponent(out OVRBounded2D bounded2D) && bounded2D.IsEnabled)
                {
                    var rect = bounded2D.BoundingBox;
                    anchorSnapshot.hasPlane = true;
                    anchorSnapshot.planeCenter = new Vector3(rect.center.x, rect.center.y, 0f);
                    anchorSnapshot.planeSize = new Vector3(rect.size.x, rect.size.y, 0f);

                    if (includePlaneBoundary && bounded2D.TryGetBoundaryPointsCount(out int count) && count > 0)
                    {
                        using var points = new NativeArray<Vector2>(count, Allocator.Temp);
                        if (bounded2D.TryGetBoundaryPoints(points))
                        {
                            for (int i = 0; i < points.Length; i++)
                            {
                                var local = new Vector3(points[i].x, points[i].y, 0f);
                                var world = worldPosition + worldRotation * local;
                                anchorSnapshot.planeBoundary.Add(world);
                            }
                        }
                    }
                }

                if (includeVolumes && anchor.TryGetComponent(out OVRBounded3D bounded3D) && bounded3D.IsEnabled)
                {
                    var bounds = bounded3D.BoundingBox;
                    anchorSnapshot.hasVolume = true;
                    anchorSnapshot.volumeCenter = bounds.center;
                    anchorSnapshot.volumeSize = bounds.size;
                }

                if (includeMeshes && anchor.TryGetComponent(out OVRTriangleMesh mesh) && mesh.IsEnabled)
                {
                    anchorSnapshot.hasMesh = true;
                    if (mesh.TryGetCounts(out int vertexCount, out int triangleCount))
                    {
                        anchorSnapshot.meshVertexCount = vertexCount;
                        anchorSnapshot.meshTriangleCount = triangleCount;
                    }
                }

                snapshot.anchors.Add(anchorSnapshot);
            }

            LastSnapshot = snapshot;
            return snapshot;
        }

        private static string ToMetaLabelString(OVRSemanticLabels.Classification classification)
        {
            switch (classification)
            {
                case OVRSemanticLabels.Classification.Floor:
                    return "FLOOR";
                case OVRSemanticLabels.Classification.Ceiling:
                    return "CEILING";
                case OVRSemanticLabels.Classification.WallFace:
                    return "WALL_FACE";
                case OVRSemanticLabels.Classification.Couch:
                    return "COUCH";
                case OVRSemanticLabels.Classification.DoorFrame:
                    return "DOOR_FRAME";
                case OVRSemanticLabels.Classification.WindowFrame:
                    return "WINDOW_FRAME";
                case OVRSemanticLabels.Classification.Other:
                    return "OTHER";
                case OVRSemanticLabels.Classification.Storage:
                    return "STORAGE";
                case OVRSemanticLabels.Classification.Bed:
                    return "BED";
                case OVRSemanticLabels.Classification.Screen:
                    return "SCREEN";
                case OVRSemanticLabels.Classification.Lamp:
                    return "LAMP";
                case OVRSemanticLabels.Classification.Plant:
                    return "PLANT";
                case OVRSemanticLabels.Classification.Table:
                    return "TABLE";
                case OVRSemanticLabels.Classification.WallArt:
                    return "WALL_ART";
                case OVRSemanticLabels.Classification.InvisibleWallFace:
                    return "INVISIBLE_WALL_FACE";
                case OVRSemanticLabels.Classification.SceneMesh:
                    return "GLOBAL_MESH";
                default:
                    return "OTHER";
            }
        }
    }
}

