using System.Globalization;

namespace Companion.Spatial.Tracking
{
    /// <summary>
    /// One thing that happened to one track, as the scanner reads it to add, move, grey out or remove a pin. A call to
    /// <see cref="TrackTable.Update"/> returns its events in a fixed order: first the time-based ones (<see cref="TrackEventKind.Lost"/>,
    /// <see cref="TrackEventKind.Retired"/>) in track id order, then one for each detection in the order of the capture.
    /// </summary>
    public readonly struct TrackEvent
    {
        internal TrackEvent(TrackEventKind kind, int trackId, double timestamp, int detectionIndex)
        {
            Kind = kind;
            TrackId = trackId;
            Timestamp = timestamp;
            DetectionIndex = detectionIndex;
        }

        /// <summary>What happened.</summary>
        public TrackEventKind Kind { get; }

        /// <summary>The id of the track it happened to (ids start at 1 and are never reused).</summary>
        public int TrackId { get; }

        /// <summary>The scan time of the call that reported it, in seconds.</summary>
        public double Timestamp { get; }

        /// <summary>
        /// For Found, Updated and Revived: the index, in the list given to <see cref="TrackTable.Update"/>, of the
        /// detection that caused it, so the scanner can give that detection's pin the track's id. -1 for Lost and
        /// Retired, which no detection causes.
        /// </summary>
        public int DetectionIndex { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} #{1} at {2}s (detection {3})", Kind, TrackId, Timestamp, DetectionIndex);
        }
    }
}
