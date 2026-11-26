using System.Collections.Generic;
using System.Threading.Tasks;
using CompanionScan.Mapping;
using UnityEngine;

namespace CompanionScan.Replay
{
    /// <summary>The scene backend of a recorded session: the recorded Space Setup read.</summary>
    public class ReplaySceneSource : MonoBehaviour, ISceneSource
    {
        /// <summary>The session being played.</summary>
        public ScanRecording Recording { get; set; }

        public MetaSceneSnapshot LastSnapshot { get; private set; }

        public Task<MetaSceneSnapshot> CaptureSnapshotAsync()
        {
            var recorded = Recording?.scene;
            if (recorded == null || recorded.anchors.Count == 0)
            {
                LastSnapshot = new MetaSceneSnapshot
                {
                    timestamp = Time.timeAsDouble,
                    error = "No scene model found in the recording."
                };
                return Task.FromResult(LastSnapshot);
            }

            // A copy, so what was played back cannot change the recording; the time is now, as a read is.
            LastSnapshot = new MetaSceneSnapshot
            {
                anchors = new List<MetaSceneAnchorSnapshot>(recorded.anchors),
                supportedLabels = new List<string>(recorded.supportedLabels),
                roomUuids = new List<string>(recorded.roomUuids),
                primaryRoomUuid = recorded.primaryRoomUuid,
                timestamp = Time.timeAsDouble
            };
            return Task.FromResult(LastSnapshot);
        }

        public List<SpatialPin> ConvertSnapshotToPins(MetaSceneSnapshot snapshot = null)
        {
            return SceneSnapshotPins.Convert(snapshot ?? LastSnapshot);
        }

        /// <summary>A recording has no meshes.</summary>
        public bool TryBuildSceneMeshes(Transform parent, Material meshMaterial, out List<GameObject> meshObjects)
        {
            meshObjects = new List<GameObject>();
            return false;
        }
    }
}
