using CompanionScan.Export;
using CompanionScan.Mapping;
using UnityEngine;

namespace CompanionScan.Replay
{
    /// <summary>
    /// The replay backends of one recording, attached to a scene's pipeline: the camera plays the
    /// recorded frames, the scene backend gives the recorded room and the raycaster meets the recorded
    /// planes. Nothing here touches a headset.
    /// </summary>
    public sealed class ReplayRig
    {
        private ReplayRig()
        {
        }

        public ScanRecording Recording { get; private set; }
        public ReplayCameraSource Camera { get; private set; }
        public ReplaySceneSource Scene { get; private set; }
        public ReplayRaycastSource Raycast { get; private set; }

        /// <summary>
        /// Adds the replay components to <paramref name="host"/> and points the pipeline (and the
        /// exporter, when given) and the raycaster at them.
        /// </summary>
        public static ReplayRig Attach(GameObject host, ScanRecording recording, SpatialCapturePipeline pipeline,
            ViewportRaycaster raycaster, SpatialMapExporter exporter = null)
        {
            var rig = new ReplayRig
            {
                Recording = recording,
                Camera = host.AddComponent<ReplayCameraSource>(),
                Scene = host.AddComponent<ReplaySceneSource>(),
                Raycast = new ReplayRaycastSource(recording.planes)
            };
            rig.Camera.Recording = recording;
            rig.Scene.Recording = recording;

            pipeline.UseCameraProvider(rig.Camera);
            pipeline.UseSceneSource(rig.Scene);
            exporter?.UseSceneSource(rig.Scene);
            raycaster.AddSource(rig.Raycast);
            return rig;
        }
    }
}
