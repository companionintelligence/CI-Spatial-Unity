namespace Companion.Spatial.Extras
{
    /// <summary>
    /// One camera frame the scan kept (a snapshot): the picture, where the camera was when it took it, and the camera's
    /// pinhole model. Objects point back to frames through <see cref="ScanObservation.FrameId"/>. The web Scan
    /// contract calls this a keyframe.
    /// </summary>
    public sealed class ScanFrame : ExtrasNode
    {
        /// <summary>Creates an empty frame.</summary>
        public ScanFrame()
        {
            Id = string.Empty;
        }

        /// <summary>The frame's id (JSON <c>id</c>): unique in the file and stable across exports; the scanner's snapshot id ("snap_0001").</summary>
        public string Id { get; set; }

        /// <summary>When the frame was taken, as an ISO 8601 UTC time ("2026-11-25T10:00:00Z"), or null when unknown (JSON <c>timestamp_utc</c>). A v1 snapshot only had the scan clock, so a migrated frame has none.</summary>
        public string TimestampUtc { get; set; }

        /// <summary>The path of the frame's JPEG beside the glTF, with forward slashes ("snapshots/snap_0001.jpg"), or null (JSON <c>image</c>).</summary>
        public string Image { get; set; }

        /// <summary>Where the camera was when it took the frame, in the space's coordinate frame, or null when unknown (JSON <c>pose</c>).</summary>
        public ScanPose Pose { get; set; }

        /// <summary>The camera's pinhole model for this frame, or null when unknown (JSON <c>intrinsics</c>).</summary>
        public ScanIntrinsics Intrinsics { get; set; }
    }
}
