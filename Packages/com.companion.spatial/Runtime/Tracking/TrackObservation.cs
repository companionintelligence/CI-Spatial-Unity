namespace Companion.Spatial.Tracking
{
    /// <summary>
    /// One sighting of a track's object: which capture frame it was in, where its box was in that frame's image, and
    /// how sure the detector was. A track keeps its most recent ones; the extras export them as the object's
    /// <c>observations</c>.
    /// </summary>
    public readonly struct TrackObservation
    {
        internal TrackObservation(string frameId, NormalizedBox? box, double score)
        {
            FrameId = frameId;
            Box = box;
            Score = score;
        }

        /// <summary>The id of the capture frame (the snapshot) the sighting is from; empty when the detection gave none.</summary>
        public string FrameId { get; }

        /// <summary>The box as fractions of that frame's image, or null when the detection had none.</summary>
        public NormalizedBox? Box { get; }

        /// <summary>The detector's score for this sighting, clamped to 0..1.</summary>
        public double Score { get; }
    }
}
