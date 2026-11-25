using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CompanionScan.Mapping
{
    /// <summary>
    /// A scene backend: where the planes, volumes and meshes of the room come from. Meta's Space Setup
    /// (through <c>MetaSceneCollector</c>), ARKit and ARCore planes, Android XR planes or a recorded
    /// session. The snapshot is expressed in Meta's semantic labels, so a backend maps its own classes to
    /// them; <see cref="SemanticLabelMapper"/> has the label list.
    /// </summary>
    public interface ISceneSource
    {
        /// <summary>The most recent snapshot, or null before the first <see cref="CaptureSnapshotAsync"/>.</summary>
        MetaSceneSnapshot LastSnapshot { get; }

        /// <summary>
        /// Reads the room. A failure (no scene model, permission denied) is reported in
        /// <see cref="MetaSceneSnapshot.error"/> rather than thrown; the snapshot is also stored in
        /// <see cref="LastSnapshot"/>.
        /// </summary>
        Task<MetaSceneSnapshot> CaptureSnapshotAsync();

        /// <summary>One <see cref="SpatialPin"/> per anchor of <paramref name="snapshot"/> (default: the last one).</summary>
        List<SpatialPin> ConvertSnapshotToPins(MetaSceneSnapshot snapshot = null);

        /// <summary>
        /// Builds a GameObject with a mesh for each anchor that has one, under <paramref name="parent"/>
        /// (for the glTF export). Returns false when there are none.
        /// </summary>
        bool TryBuildSceneMeshes(Transform parent, Material meshMaterial, out List<GameObject> meshObjects);
    }

    /// <summary>Turns a scene snapshot into pins; the same for every <see cref="ISceneSource"/>.</summary>
    public static class SceneSnapshotPins
    {
        /// <summary>
        /// One pin per anchor with <c>source = PinSource.MetaScene</c>, so the scene backend and the vision
        /// server write to a single <see cref="SpatialPinManager"/>. The first label names the pin (OTHER
        /// when there is none); the score is 1, as a scene model is taken as ground truth.
        /// </summary>
        public static List<SpatialPin> Convert(MetaSceneSnapshot snapshot)
        {
            var pins = new List<SpatialPin>();
            if (snapshot?.anchors == null)
            {
                return pins;
            }

            foreach (var anchor in snapshot.anchors)
            {
                // Use the first label, or fall back to OTHER
                string label = anchor.labels != null && anchor.labels.Count > 0
                    ? anchor.labels[0]
                    : "OTHER";

                pins.Add(new SpatialPin
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
                });
            }

            return pins;
        }
    }
}
