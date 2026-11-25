namespace Companion.Spatial.Tracking
{
    /// <summary>What happened to a track in one <see cref="TrackTable.Update"/> or <see cref="TrackTable.Tick"/>.</summary>
    public enum TrackEventKind
    {
        /// <summary>A detection matched no track, so a new track (and a new pin) was made.</summary>
        Found,

        /// <summary>A detection matched an active track: its position, confidence and history were updated.</summary>
        Updated,

        /// <summary>
        /// An active track was not updated for the lost time. Reported once, by the next update or tick; the track
        /// stays in the table.
        /// </summary>
        Lost,

        /// <summary>A detection matched a lost track: it is active again, with the same id.</summary>
        Revived,

        /// <summary>
        /// A track was removed because it had not been seen for the retire time (never, unless the options set one).
        /// Its id is not used again.
        /// </summary>
        Retired
    }
}
