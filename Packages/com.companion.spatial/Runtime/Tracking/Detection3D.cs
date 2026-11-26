namespace Companion.Spatial.Tracking
{
    /// <summary>
    /// One detection that already has a place in the room: the label a detector gave it, its score, and the world
    /// position the app raycast it to. The scanner makes one of these per accepted box of a capture (from
    /// <c>SpatialCapturePipeline</c>'s <c>hitPoint</c> and <c>hitNormal</c>) and hands the whole capture to
    /// <see cref="TrackTable.Update"/>.
    /// </summary>
    public readonly struct Detection3D
    {
        /// <summary>Creates a detection.</summary>
        /// <param name="label">The detector's raw text for the object ("chair", "TV screen", "Sofa Bed"); the vocabulary turns it into a tag.</param>
        /// <param name="score">The detector's confidence, 0 to 1. A value outside that range is clamped; NaN makes the table ignore the detection.</param>
        /// <param name="position">Where the raycast put the object, in world metres. It must be finite.</param>
        /// <param name="normal">The surface normal at the hit, or <see cref="Vec3.Zero"/> when there is none.</param>
        /// <param name="frameId">The id of the capture frame the detection came from (the snapshot id); null counts as empty.</param>
        /// <param name="box">The detection's box as fractions of the frame image, or null when the box is unknown or unusable.</param>
        public Detection3D(string label, double score, Vec3 position, Vec3 normal, string frameId, NormalizedBox? box = null)
        {
            Label = label;
            Score = score;
            Position = position;
            Normal = normal;
            FrameId = frameId;
            Box = box;
        }

        /// <summary>The detector's raw text for the object.</summary>
        public string Label { get; }

        /// <summary>The detector's confidence, 0 to 1 (before clamping).</summary>
        public double Score { get; }

        /// <summary>Where the raycast put the object, in world metres.</summary>
        public Vec3 Position { get; }

        /// <summary>The surface normal at the hit; zero when there is none.</summary>
        public Vec3 Normal { get; }

        /// <summary>The id of the capture frame the detection came from.</summary>
        public string FrameId { get; }

        /// <summary>The detection's box as fractions of the frame image, or null.</summary>
        public NormalizedBox? Box { get; }
    }
}
