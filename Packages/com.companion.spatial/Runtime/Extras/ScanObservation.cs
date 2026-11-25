namespace Companion.Spatial.Extras
{
    /// <summary>
    /// One sighting of an object: the capture frame it was in, its 2D box in that frame's image, and the detector's
    /// score. An object that was seen in several captures has several; they let a viewer show which frames to look at.
    /// </summary>
    public sealed class ScanObservation : ExtrasNode
    {
        /// <summary>The <see cref="ScanFrame.Id"/> of the frame the object was seen in (JSON <c>frame_id</c>).</summary>
        public string FrameId { get; set; }

        /// <summary>
        /// The box as fractions of that frame's image, or null when there is none (JSON <c>bbox_cxcywh</c>, the web Scan
        /// contract's [cx, cy, w, h]).
        /// </summary>
        public NormalizedBox? Box { get; set; }

        /// <summary>The detector's score for this sighting, 0 to 1 (JSON <c>score</c>).</summary>
        public double Score { get; set; }
    }
}
