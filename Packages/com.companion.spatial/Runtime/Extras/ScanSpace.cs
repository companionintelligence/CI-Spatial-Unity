namespace Companion.Spatial.Extras
{
    /// <summary>
    /// The room (or space) the file describes: its id, the coordinate frame every position in the file is in, and when
    /// the file was first and last exported. The v1 extras had the id as <c>space_id</c> and the last time as
    /// <c>timestamp_utc</c>.
    /// </summary>
    public sealed class ScanSpace : ExtrasNode
    {
        /// <summary>The id used when the scanner knows no room: the same fallback as the exporter's file name ("default").</summary>
        public const string DefaultId = "default";

        /// <summary>The frame of a Unity scene: metres, +Y up, left-handed (+Z forward). What the Unity scanner writes.</summary>
        public const string UnityWorldFrame = "unity-world";

        /// <summary>The frame of the web Scan contract: WebXR <c>local-floor</c>, metres, +Y up, right-handed, origin on the floor under the headset at session start.</summary>
        public const string LocalFloorFrame = "local-floor";

        /// <summary>Creates a space with the default id and the Unity world frame.</summary>
        public ScanSpace()
        {
            Id = DefaultId;
            Frame = UnityWorldFrame;
        }

        /// <summary>The space's id (JSON <c>id</c>): the Space Setup room uuid, or "default". Re-exports of one space share it.</summary>
        public string Id { get; set; }

        /// <summary>The coordinate frame of every position, rotation and size in the file (JSON <c>frame</c>): <see cref="UnityWorldFrame"/> or <see cref="LocalFloorFrame"/>.</summary>
        public string Frame { get; set; }

        /// <summary>When the file was first exported, as an ISO 8601 UTC time, or null when unknown (JSON <c>created_at_utc</c>).</summary>
        public string CreatedAtUtc { get; set; }

        /// <summary>When the file was last exported, as an ISO 8601 UTC time, or null when unknown (JSON <c>updated_at_utc</c>); v1's <c>timestamp_utc</c>.</summary>
        public string UpdatedAtUtc { get; set; }
    }
}
