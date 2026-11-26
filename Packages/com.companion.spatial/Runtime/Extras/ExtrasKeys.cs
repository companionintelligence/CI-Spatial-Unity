namespace Companion.Spatial.Extras
{
    /// <summary>
    /// The JSON keys of each part of the v2 document, in one place for the reader and the writer. The reader keeps every
    /// other key of a part in its <see cref="ExtrasNode.Unknown"/> bag; the writer puts the unknown keys back after the
    /// known ones and never writes one under a name listed here, so a stray key can neither overwrite a part nor turn a
    /// part that was left out (no consent, no bounds) into a value of the wrong type. The arrays are never changed.
    /// </summary>
    internal static class ExtrasKeys
    {
        internal static readonly string[] Root = { "schema", "schema_version", "units", "space", "consent", "device", "frames", "objects", "planes", "bounds" };

        internal static readonly string[] Space = { "id", "frame", "created_at_utc", "updated_at_utc" };

        internal static readonly string[] Consent = { "notice_version", "agreed_at_utc", "server" };

        internal static readonly string[] Device = { "model", "os", "app", "app_version" };

        internal static readonly string[] Frame = { "id", "timestamp_utc", "image", "pose", "intrinsics" };

        // "rotation" is the name the reader also accepts for the quaternion.
        internal static readonly string[] Pose = { "position", "quaternion", "rotation" };

        internal static readonly string[] Intrinsics = { "fx", "fy", "cx", "cy", "width", "height" };

        internal static readonly string[] Bounds = { "center", "size" };

        internal static readonly string[] Obj = { "id", "track_id", "label", "confidence", "source", "pose", "normal", "bounds", "anchor_uuid", "observations" };

        internal static readonly string[] Label = { "text", "tag", "semantic" };

        internal static readonly string[] Observation = { "frame_id", "bbox_cxcywh", "score" };

        internal static readonly string[] Plane = { "id", "label", "semantic", "pose", "center", "size", "boundary" };
    }
}
