namespace Companion.Spatial.Tracking
{
    /// <summary>Whether the scanner has seen a track's object recently.</summary>
    public enum TrackState
    {
        /// <summary>Seen within the table's lost time (default 10 s of scan time).</summary>
        Active,

        /// <summary>
        /// Not seen for the lost time. The track stays in the table, so a later capture of the same object revives it
        /// instead of pinning it a second time.
        /// </summary>
        Lost
    }
}
